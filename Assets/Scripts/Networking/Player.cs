using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour {
	private TankController selectedTank;
	private TankOccupation tankPosition;

	void Start() {

	}

	void Update() {
		if (isClient) {
			if (!selectedTank) {
				//TODO: temporary hotkey tank assignment
				if (Input.GetKeyDown(KeyCode.Alpha1)) {
					//choose first available driver slot
					ChooseTank(TankOccupation.Driver);
				} else if (Input.GetKeyDown(KeyCode.Alpha2)) {
					//choose first available gunner slot
					ChooseTank(TankOccupation.Gunner);
				}
			} else {
				Controls();
			}
		}
	}

	[Client]
	private void ChooseTank(TankOccupation occupation) {
		//vacate existing position
		if (selectedTank != null) {
			CmdVacateTank(selectedTank);
		}
		foreach (TankController t in NetworkingController.instance.GetTanks().Values) {
			//join first tank with open position
			if (t.availableOccupations[(int)occupation]) {
				CmdOccupyTank(t, occupation);

				tankPosition = occupation;
				selectedTank = t;
				break;
			}
		}
	}
	[Command]
	private void CmdVacateTank(TankController t) {
		t.VacatePosition(tankPosition);
		t.netIdentity.RemoveClientAuthority();
	}
	[Command]
	private void CmdOccupyTank(TankController t, TankOccupation occupation) {
		//give client authority to call commands
		t.OccupyPosition(occupation);
		t.netIdentity.AssignClientAuthority(connectionToClient);
	}

	//if velocity is the same, don't call command function
	private Vector3 currentVelocity = Vector3.zero;
	private float currentRotation = 0;

	private float turretRotation = 0, deltaTurretRotation = 0;

	[Client]
	private void Controls() {
		float newRotation = 0;
		Vector3 newVelocity = Vector3.zero;

		//TODO: split roles of tank by driver and gunner, etc and lock FOV
		if (Input.GetKey(KeyCode.W)) {
			newVelocity += Vector3.forward;
		}
		if (Input.GetKey(KeyCode.S)) {
			newVelocity += Vector3.back;
		}
		if (Input.GetKey(KeyCode.A)) {
			newRotation -= 1;
		}
		if (Input.GetKey(KeyCode.D)) {
			newRotation += 1;
		}

		float newTurretRotation = 0;
		if (Input.GetKey(KeyCode.Q)) {
			newTurretRotation -= 30;
		}
		if (Input.GetKey(KeyCode.E)) {
			newTurretRotation += 30;
		}

		//change rotation on server side
		if (currentRotation != newRotation) {
			selectedTank.SetTankRotation(newRotation);
			currentRotation = newRotation;
		}

		//change turret rotation on server side
		if (newTurretRotation != deltaTurretRotation) {
			turretRotation += newTurretRotation;
			selectedTank.SetTargetTurretRotation(turretRotation);
			deltaTurretRotation = newTurretRotation;
		}

		//change velocity on server side
		if (currentVelocity != newVelocity) {
			selectedTank.SetTankVelocity(newVelocity);
			currentVelocity = newVelocity;
		}
	}
}
