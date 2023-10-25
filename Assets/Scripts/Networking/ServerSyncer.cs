using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <Summary> Creates a bridge of SyncVars between the server and the clients without
/// relying on any singular player. For example, instead of calling ClientRPC every frame
/// on every player to sync information, systemically store all server information the
/// clients need upon joining the game here for cleaner code. </Summary>

public class ServerSyncer : NetworkBehaviour {
	//syncs to clients which tanks are available to drive
	public static ServerSyncer instance;

	[SyncVar]
	public string clientTanks; //TODO: temporary display of available tanks for clients

	private void Awake() {
		instance = this;
	}
	private void Update() {
		if (isClient) {
			UIManager.instance.tanksText.text = clientTanks;
		}
	}
}