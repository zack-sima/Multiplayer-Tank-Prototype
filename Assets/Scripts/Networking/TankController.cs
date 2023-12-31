using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Mirror;

// NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake. You can do that in Start instead.
// NOTE: Start and stop authority will not be used

[RequireComponent(typeof(Rigidbody))]
public class TankController : NetworkBehaviour {
	[SyncVar]
	private float wheelVelocity; //animate wheels on client side when moving
	[SyncVar]
	public float displaySpeed; //client side displayed on speedometer
	[SyncVar]
	public int tankAmmo, machineGunAmmo; //infinite, just the ones in magazine
	[SyncVar]
	public float health;

	public const float maxHealth = 1000; //TODO: set custom

	//prefabs
	[SerializeField]
	private GameObject bulletPrefab, explosionPrefab; //death explosion

	//tank parts
	[SerializeField]
	private Transform healthCanvas;
	[SerializeField]
	private RectTransform healthBar;
	[SerializeField]
	private Camera gunnerCamera, driverCamera;
	[SerializeField]
	private Transform turret, turretTop, barrel, shootAnchor;
	[SerializeField]
	private List<Transform> wheels;

	public readonly SyncList<bool> availableOccupations = new();
	private List<Player> occupiers = new();

	private Vector3 originalBarrelPosition;
	private float barrelBackAmount = 0f; //client animation

	private float turretXRotation = 0f; //track rotation to bound between angles
	private const float maxTurretRotation = 15f;

	private Rigidbody rb;

	#region Start & Stop Callbacks

	public override void OnStartServer() {
		NetworkingController.instance.AddTank(this);
		for (int i = 0; i < System.Enum.GetValues(typeof(TankOccupation)).Length; i++) {
			availableOccupations.Add(true);
			occupiers.Add(null);
		}
		rb.centerOfMass += Vector3.down * 0.7f;
		health = maxHealth;

		//initialize ammo
		tankAmmo = 1;
		machineGunAmmo = 50;

		transform.SetPositionAndRotation(NetworkingController.instance.FindSpawnpoint(false),
			Quaternion.identity);
	}

	public override void OnStopServer() {
		NetworkingController.instance.RemoveTank(netId);
	}

	public override void OnStartClient() {
		//don't check anything on client rigidbody
		if (isClientOnly) {
			rb.isKinematic = true;
			NetworkingController.instance.AddTank(this);
		}
	}

	public override void OnStopClient() {
		if (isClientOnly) {
			NetworkingController.instance.RemoveTank(netId);
		}
	}

	public override void OnStartLocalPlayer() { }

	public override void OnStopLocalPlayer() { }

	#endregion

	//server variables
	private Vector2 targetTurretRotation = Vector2.zero;

	private float targetTankRotationChange = 0;
	private float currentTankRotationChange = 0; //accelerate when rotating

	private Vector3 currentTankTargetVelocity = Vector3.zero;

	//gradually interpolates towards target for acceleration
	private Vector3 currentTankRealVelocity = Vector3.zero;

	//server only, for reloading tank
	private float reloadCountdown = 0f;
	private const float reloadSpeed = 3f;

	private const float tankSpeed = 5, rotationSpeed = 35, turretRotationSpeed = 35; //TODO: set based on vehicle

	private void Awake() {
		rb = GetComponent<Rigidbody>();
		originalBarrelPosition = barrel.localPosition;
	}


	private void Update() {
		if (isServer) {
			//reload ammo
			if (reloadCountdown > 0) {
				reloadCountdown -= Time.deltaTime;
				if (reloadCountdown <= 0) {
					tankAmmo = 1;
				}
			} else if (tankAmmo <= 0) {
				reloadCountdown += reloadSpeed;
			}

			ServerMovements();
		}
		if (isClient) {
			ClientAnimations();

			//don't display health bar if local
			bool active = true;
			foreach (Player p in occupiers) {
				if (p != null && p.isLocalPlayer) {
					active = false;
					break;
				}
			}
			if (active != healthCanvas.gameObject.activeInHierarchy) {
				healthCanvas.gameObject.SetActive(active);
			}
		}
	}
	private void FixedUpdate() {
		if (Camera.main != null) {
			healthCanvas.rotation = Camera.main.transform.rotation;
		}
	}

	//getters
	public Transform GetTurret() {
		return turret;
	}

	public void UpdateHealth() {
		healthBar.localScale = new Vector2(health / maxHealth, 1);
	}

	[Client]
	private void ClientAnimations() {
		if (barrelBackAmount > 0) {
			barrelBackAmount = Mathf.Max(barrelBackAmount -= Time.deltaTime, 0f);
		}
		barrel.localPosition = originalBarrelPosition + new Vector3(0, 0, -barrelBackAmount);

		foreach (Transform t in wheels) {
			t.Rotate(0, wheelVelocity * Time.deltaTime * 300, 0);
		}
	}

	//for local players controlling tank
	[Client]
	public void DisableCameras() {
		driverCamera.gameObject.SetActive(false);
		gunnerCamera.gameObject.SetActive(false);
	}
	[Client]
	public void EnableCamera(TankOccupation p) {
		if (p == TankOccupation.Driver) {
			driverCamera.gameObject.SetActive(true);
		} else if (p == TankOccupation.Gunner) {
			gunnerCamera.gameObject.SetActive(true);
		}
	}

	[Server]
	private void ServerMovements() {
		//interpolate tank acceleration, faster acceleration than de-acceleration
		bool isAccelerating = currentTankRealVelocity.sqrMagnitude < currentTankTargetVelocity.sqrMagnitude;
		currentTankRealVelocity = Vector3.MoveTowards(currentTankRealVelocity,
			currentTankTargetVelocity, Time.deltaTime * (isAccelerating ? 1.5f : 3f));

		bool isRotationAccelerating = targetTankRotationChange != 0;
		if (currentTankRotationChange < targetTankRotationChange) {
			currentTankRotationChange = Mathf.Min(targetTankRotationChange,
				currentTankRotationChange + Time.deltaTime * (isRotationAccelerating ? 2.5f : 3.5f));
		} else if (currentTankRotationChange > targetTankRotationChange) {
			currentTankRotationChange = Mathf.Max(targetTankRotationChange,
				currentTankRotationChange - Time.deltaTime * (isRotationAccelerating ? 2.5f : 3.5f));
		}

		//animate wheels
		wheelVelocity = currentTankRealVelocity.z;

		//change display speed
		displaySpeed = currentTankRealVelocity.sqrMagnitude;

		//rotate towards target rotation (if occupied)
		if (!availableOccupations[(int)TankOccupation.Gunner]) {
			turret.Rotate(0, targetTurretRotation.x * Time.deltaTime * turretRotationSpeed, 0);

			turretXRotation = Mathf.Clamp(turretXRotation + targetTurretRotation.y * Time.deltaTime * turretRotationSpeed,
				-maxTurretRotation, maxTurretRotation);

			turretTop.localEulerAngles = new Vector3(turretXRotation, 0, 0);
		}

		//if driver is vacant reset movement
		if (availableOccupations[(int)TankOccupation.Driver]) {
			currentTankTargetVelocity = Vector3.zero;
			currentTankRotationChange = 0;
		}

		transform.Rotate(0, currentTankRotationChange * rotationSpeed * Time.deltaTime, 0);
		Vector3 targetVelocity = transform.TransformDirection(currentTankRealVelocity * tankSpeed); ;

		//keep gravity
		rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);
	}
	[Server]
	public void TankHit(Vector3 impactPoint, TankController sender, float damage, bool shakeTank) {
		health -= damage;
		if (health <= 0) {
			//TODO: blow up tank
			RpcExplodeTank(transform.position + Vector3.up * 0.5f);
			transform.SetPositionAndRotation(NetworkingController.instance.FindSpawnpoint(false),
				Quaternion.identity);
			turret.localRotation = Quaternion.identity;

			Debug.Log($"tank_{netId} blown up by tank_{sender.netId}!");
			health = maxHealth;

			//exit crew
			for (int i = 0; i < occupiers.Count; i++) {
				if (occupiers[i] != null) {
					occupiers[i].ForceVacateTank();
				}
			}
			//don't shake tank if blowing up
			shakeTank = false;
		}
		if (shakeTank) {
			float force = 50f;
			rb.AddExplosionForce(force * rb.mass, new Vector3(impactPoint.x,
				transform.position.y - 1f, impactPoint.z), 5f);
		}
		RpcTankHit();
	}
	[ClientRpc]
	public void RpcExplodeTank(Vector3 position) {
		Destroy(Instantiate(explosionPrefab, position, Quaternion.identity), 7f);
	}
	[ClientRpc]
	public void RpcTankHit() {
		Invoke(nameof(UpdateHealth), 0.05f);
	}

	[Server]
	public void OccupyPosition(TankOccupation occupation, Player player) {
		availableOccupations[(int)occupation] = false;
		occupiers[(int)occupation] = player;
		NetworkingController.instance.UpdateTankString();
	}
	[Server]
	public void VacatePosition(TankOccupation occupation) {
		availableOccupations[(int)occupation] = true;
		occupiers[(int)occupation] = null;
		NetworkingController.instance.UpdateTankString();
	}
	[Server]
	public void ShootBullet() {
		if (tankAmmo <= 0) return;

		tankAmmo--;

		GameObject bullet = Instantiate(bulletPrefab, shootAnchor.position, shootAnchor.rotation);
		bullet.GetComponent<TankProjectile>().sender = this;
		NetworkServer.Spawn(bullet);

		float force = 15f;
		Vector3 impactPoint = shootAnchor.position;
		rb.AddExplosionForce(force * rb.mass, new Vector3(impactPoint.x,
			transform.position.y - 1f, impactPoint.z), 5f);

		RpcShootBullet();
	}
	[ClientRpc]
	private void RpcShootBullet() {
		//animate tank barrel going back
		StartCoroutine(AddBarrelRecoil());
	}
	private IEnumerator AddBarrelRecoil() {
		for (float i = 0f; i < 0.05f; i += Time.deltaTime) {
			barrelBackAmount = Mathf.Min(barrelBackAmount + Time.deltaTime * 7.5f, 0.35f);
			yield return null;
		}
	}

	[Server] //instead of moving tank per frame, set velocity when direction changes
	public void SetTankVelocity(Vector3 velocity) {
		currentTankTargetVelocity = velocity;
	}
	[Server] //set tank rotation (left, right, or no rotation)
	public void SetTankRotation(float direction) {
		targetTankRotationChange = direction;
	}
	[Server] //only y rotation; might use target position instead and calculate on server
	public void SetTargetTurretRotation(Vector2 rotation) {
		targetTurretRotation = rotation;
	}
}
[System.Serializable]
public enum TankOccupation { Driver, Gunner }