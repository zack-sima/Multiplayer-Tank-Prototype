using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour {
	private TankController selectedTank;
	private TankOccupation tankPosition;

	private int selectedTankIndex = 0; //TODO: currently use arrow keys to choose wanted tank

	void Update() {
		if (isClient && isLocalPlayer) {
			//TODO: temporary hotkey tank assignment
			if (Input.GetKeyDown(KeyCode.UpArrow)) {
				selectedTankIndex = Mathf.Max(selectedTankIndex - 1, 0);
			}
			if (Input.GetKeyDown(KeyCode.DownArrow)) {
				selectedTankIndex = Mathf.Min(selectedTankIndex + 1,
					NetworkingController.instance.GetTanks().Values.Count - 1);
			}
			UIManager.instance.playerJoinText.text = "";
			for (int i = 0; i < selectedTankIndex; i++) UIManager.instance.playerJoinText.text += "\n";
			UIManager.instance.playerJoinText.text += "< Join";

			if (Input.GetKeyDown(KeyCode.Alpha1)) {
				//choose first available driver slot
				ChooseTank(TankOccupation.Driver);
			} else if (Input.GetKeyDown(KeyCode.Alpha2)) {
				//choose first available gunner slot
				ChooseTank(TankOccupation.Gunner);
			}
			if (selectedTank != null) {
				Controls();
			}
		}
	}

	//** called by server when player left game **
	[Server]
	public void PlayerLeftGame() {
		if (selectedTank != null) selectedTank.VacatePosition(tankPosition);
	}

	[Client]
	private void ChooseTank(TankOccupation occupation) {
		List<TankController> tanks = new(NetworkingController.instance.GetTanks().Values);
		tanks.OrderBy(t => t.netId);
		TankController t = tanks[selectedTankIndex];

		//if hovering tank has position, join
		if (t.availableOccupations[(int)occupation]) {

			//vacate existing position
			if (selectedTank != null) {
				CmdVacateTank();
			}

			CmdOccupyTank(t, occupation);
			CmdSetTank(t, occupation);

			tankPosition = occupation;
			selectedTank = t;
		}
	}
	[Command]
	private void CmdSetTank(TankController t, TankOccupation o) {
		selectedTank = t;
		tankPosition = o;
	}
	[Command]
	private void CmdVacateTank() {
		selectedTank.VacatePosition(tankPosition);
	}
	[Command]
	private void CmdOccupyTank(TankController t, TankOccupation occupation) {
		t.OccupyPosition(occupation, this);
	}

	//if velocity is the same, don't call command function
	private Vector3 currentVelocity = Vector3.zero;
	private float currentRotation = 0, deltaTurretRotation = 0;

	[Command]
	private void CmdSetTankVelocity(Vector3 newVelocity) {
		selectedTank.SetTankVelocity(newVelocity);
	}
	[Command]
	private void CmdSetTankRotation(float newRotation) {
		selectedTank.SetTankRotation(newRotation);
	}
	[Command]
	private void CmdSetTargetTurretRotation(float newRotation) {
		selectedTank.SetTargetTurretRotation(newRotation);
	}
	[Command]
	private void CmdShootBullet() {
		selectedTank.ShootBullet();
	}

	[Client]
	private void Controls() {
		float newRotation = 0;
		Vector3 newVelocity = Vector3.zero;

		if (tankPosition == TankOccupation.Driver) {
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
			//change velocity on server side
			if (currentVelocity != newVelocity) {
				CmdSetTankVelocity(newVelocity);
				currentVelocity = newVelocity;
			}
			//change rotation on server side
			if (currentRotation != newRotation) {
				CmdSetTankRotation(newRotation);
				currentRotation = newRotation;
			}
		} else if (tankPosition == TankOccupation.Gunner) {
			//aim turret towards mouse position
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out RaycastHit hit)) {
				Vector3 direction = hit.point - selectedTank.transform.position;

				float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

				//change turret rotation on server side
				if (Mathf.Abs(angle - deltaTurretRotation) > 0.01f) {
					CmdSetTargetTurretRotation(angle);
					deltaTurretRotation = angle;
				}
			}
			if (Input.GetMouseButtonDown(0)) {
				CmdShootBullet();
			}
		}
	}
}
