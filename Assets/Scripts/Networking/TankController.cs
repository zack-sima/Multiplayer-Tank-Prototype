using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Mirror;

// NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake. You can do that in Start instead.
// NOTE: Start and stop authority will not be used

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(NetworkTransform))]
public class TankController : NetworkBehaviour {
	[SyncVar]
	private float wheelVelocity; //animate wheels on client side when moving

	[SerializeField]
	private GameObject bulletPrefab;
	[SerializeField]
	private Transform turret, barrel, shootAnchor;
	[SerializeField]
	private List<Transform> wheels;

	public readonly SyncList<bool> availableOccupations = new();
	private List<Player> occupiers = new();

	private Vector3 originalBarrelPosition;
	private float barrelBackAmount = 0f; //client animation

	private Rigidbody rb;

	#region Start & Stop Callbacks

	public override void OnStartServer() {
		NetworkingController.instance.AddTank(this);
		for (int i = 0; i < System.Enum.GetValues(typeof(TankOccupation)).Length; i++) {
			availableOccupations.Add(true);
			occupiers.Add(null);
		}
		rb.centerOfMass += Vector3.down * 0.7f;
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
	private float targetTurretRotation = 0;

	private float targetTankRotationChange = 0;
	private float currentTankRotationChange = 0; //accelerate when rotating

	private Vector3 currentTankTargetVelocity = Vector3.zero;

	//gradually interpolates towards target for acceleration
	private Vector3 currentTankRealVelocity = Vector3.zero;

	private float tankSpeed = 5, rotationSpeed = 35, turretRotationSpeed = 35; //TODO: set based on vehicle

	private void Awake() {
		rb = GetComponent<Rigidbody>();
		originalBarrelPosition = barrel.localPosition;
	}
	private void Update() {
		if (isServer) {
			ServerMovements();
		}
		if (isClient) {
			ClientAnimations();
		}
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

		//rotate towards target rotation (if occupied)
		if (!availableOccupations[(int)TankOccupation.Gunner]) {
			turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.Euler(0, targetTurretRotation, 0),
				turretRotationSpeed * Time.deltaTime);
		}

		//if driver is vacant reset movement
		if (availableOccupations[(int)TankOccupation.Driver]) {
			currentTankTargetVelocity = Vector3.zero;
			currentTankRotationChange = 0;
		}

		transform.Rotate(0, currentTankRotationChange * rotationSpeed * Time.deltaTime, 0);
		rb.velocity = transform.TransformDirection(currentTankRealVelocity * tankSpeed);
	}
	[Server]
	public void TankHit(Vector3 impactPoint, TankController sender, float damage, bool shakeTank) {
		if (shakeTank) {
			if (hitCoroutine != null) StopCoroutine(hitCoroutine);
			hitCoroutine = StartCoroutine(GradualForceAtPoint(impactPoint, 1000f));
		}
	}
	private Coroutine hitCoroutine = null;
	private IEnumerator GradualForceAtPoint(Vector3 impactPoint, float force) {
		for (float i = 0; i < 0.2f; i += Time.deltaTime) {
			rb.AddExplosionForce(force * rb.mass * Time.deltaTime, new Vector3(impactPoint.x,
				transform.position.y - 1f, impactPoint.z), 5f);
			yield return null;
		}
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
		GameObject bullet = Instantiate(bulletPrefab, shootAnchor.position, shootAnchor.rotation);
		bullet.GetComponent<TankProjectile>().sender = this;
		NetworkServer.Spawn(bullet);

		if (hitCoroutine != null) StopCoroutine(hitCoroutine);
		hitCoroutine = StartCoroutine(GradualForceAtPoint(shootAnchor.position, 700f));

		RpcShootBullet();
	}
	[ClientRpc]
	private void RpcShootBullet() {
		//animate tank barrel going back
		StartCoroutine(AddBarrelRecoil());
	}
	private IEnumerator AddBarrelRecoil() {
		for (float i = 0f; i < 0.05f; i += Time.deltaTime) {
			barrelBackAmount = Mathf.Min(barrelBackAmount + Time.deltaTime * 10f, 0.5f);
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
	public void SetTargetTurretRotation(float yRotation) {
		targetTurretRotation = yRotation;
	}
}
[System.Serializable]
public enum TankOccupation { Driver, Gunner }