using System.Collections.Generic;
using UnityEngine;
using Mirror;

// NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake. You can do that in Start instead.
// NOTE: Start and stop authority will not be used

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(NetworkTransform))]
public class TankController : NetworkBehaviour {
	[SyncVar]
	private float wheelVelocity; //animate wheels on client side when moving

	[SerializeField]
	private Transform turret, barrel;
	[SerializeField]
	private List<Transform> wheels;

	public readonly SyncList<bool> availableOccupations = new();

	private Rigidbody rb;

	#region Start & Stop Callbacks

	public override void OnStartServer() {
		NetworkingController.instance.AddTank(this);
		for (int i = 0; i < System.Enum.GetValues(typeof(TankOccupation)).Length; i++) {
			availableOccupations.Add(true);
		}
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

	private float tankSpeed = 5, rotationSpeed = 50, turretRotationSpeed = 35; //TODO: set based on vehicle

	private void Awake() {
		rb = GetComponent<Rigidbody>();
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

		//rotate towards target rotation
		turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.Euler(0, targetTurretRotation, 0),
			turretRotationSpeed * Time.deltaTime);
		transform.Rotate(0, currentTankRotationChange * rotationSpeed * Time.deltaTime, 0);
		rb.velocity = transform.TransformDirection(currentTankRealVelocity * tankSpeed);
	}

	[Server]
	public void OccupyPosition(TankOccupation occupation) {
		availableOccupations[(int)occupation] = false;
	}
	[Server]
	public void VacatePosition(TankOccupation occupation) {
		availableOccupations[(int)occupation] = true;
	}
	[Command] //instead of moving tank per frame, set velocity when direction changes
	public void SetTankVelocity(Vector3 velocity) {
		currentTankTargetVelocity = velocity;
	}
	[Command] //set tank rotation (left, right, or no rotation)
	public void SetTankRotation(float direction) {
		targetTankRotationChange = direction;
	}

	[Command] //only y rotation; might use target position instead and calculate on server
	public void SetTargetTurretRotation(float yRotation) {
		targetTurretRotation = yRotation;
	}
}
[System.Serializable]
public enum TankOccupation { Driver, Gunner }