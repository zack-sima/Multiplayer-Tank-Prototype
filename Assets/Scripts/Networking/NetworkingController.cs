using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using kcp2k;

/* Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html */

public class NetworkingController : NetworkManager {
	public static NetworkingController instance;

	[SerializeField]
	private GameObject serverSyncerPrefab;

	[SerializeField]
	private GameObject t34Prefab, panzer4Prefab;

	//NOTE: both server and clients use this but separately (client self-populates this list)
	private Dictionary<uint, TankController> tanks = new();

	public Dictionary<uint, TankController> GetTanks() {
		return tanks;
	}

	//both server and clients use
	public void AddTank(TankController t) {
		tanks.Add(t.netId, t);
	}
	public void RemoveTank(uint tankId) {
		tanks.Remove(tankId);
	}

	//TODO: temporary tank positions display
	[Server]
	public void UpdateTankString() {
		Dictionary<bool, string> p = new() { { true, "open" }, { false, "closed" } };
		string s = "";
		int count = 1;
		foreach (TankController t in tanks.Values.OrderBy(t => t.netId)) {
			s += $"Tank {count}: driver(1) [{p[t.availableOccupations[0]]}], gunner(2) [{p[t.availableOccupations[1]]}]\n";
			count++;
		}

		ServerSyncer.instance.clientTanks = s;
	}

	public override void Awake() {
		base.Awake();
		instance = this;
	}
	public override void Start() {
		base.Start();
		//#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR //linux server
		//		GetComponent<KcpTransport>().Port = 7777;
		//		print("started linux server");
		//		StartServer();
		//#endif
	}

	#region Unity Callbacks

	public override void LateUpdate() {
		base.LateUpdate();
	}

	public override void OnDestroy() {
		base.OnDestroy();
	}

	#endregion

	#region Start & Stop

	/// Set the frame rate for a headless server.
	public override void ConfigureHeadlessFrameRate() {
		base.ConfigureHeadlessFrameRate();
	}

	/// called when quitting the application
	public override void OnApplicationQuit() {
		base.OnApplicationQuit();
	}

	#endregion

	#region Scene Management

	/// This causes the server to switch scenes and sets the networkSceneName
	public override void ServerChangeScene(string newSceneName) {
		base.ServerChangeScene(newSceneName);
	}

	//Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed
	public override void OnServerChangeScene(string newSceneName) { }

	//Called on the server when a scene is completed loaded when the
	//scene load was initiated by the server with ServerChangeScene().
	public override void OnServerSceneChanged(string sceneName) { }

	/// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
	public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }

	/// Called on clients when a scene has completed loaded
	public override void OnClientSceneChanged() {
		base.OnClientSceneChanged();
	}

	#endregion

	#region Server System Callbacks

	//called on the server when a new client connects.
	public override void OnServerConnect(NetworkConnectionToClient conn) { }

	//called on the server when a client is ready.
	public override void OnServerReady(NetworkConnectionToClient conn) {
		base.OnServerReady(conn);
	}

	//called on server when client connects (base function spawns prefab)
	public override void OnServerAddPlayer(NetworkConnectionToClient conn) {
		base.OnServerAddPlayer(conn);
	}

	//called on server when client disconnects
	public override void OnServerDisconnect(NetworkConnectionToClient conn) {
		conn.identity.GetComponent<Player>().PlayerLeftGame();
		base.OnServerDisconnect(conn);
	}

	public override void OnServerError(NetworkConnectionToClient conn, TransportError transportError, string message) { }

	#endregion

	#region Client System Callbacks

	public override void OnClientConnect() {
		base.OnClientConnect();
	}

	public override void OnClientDisconnect() { }

	public override void OnClientNotReady() { }

	public override void OnClientError(TransportError transportError, string message) { }

	#endregion

	#region Start & Stop Callbacks

	// Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
	// their functionality, users would need override all the versions. Instead these callbacks are invoked
	// from all versions, so users only need to implement this one case.

	public override void OnStartHost() { }

	public override void OnStartServer() {
		//spawn server syncer (**MUST SPAWN**)
		NetworkServer.Spawn(Instantiate(serverSyncerPrefab));

		//spawn tanks (right now just two tanks)
		GameObject insItem = Instantiate(t34Prefab, new Vector3(-3.5f, 0, -3), Quaternion.Euler(0, 90, 0));
		NetworkServer.Spawn(insItem);

		GameObject insItem2 = Instantiate(panzer4Prefab, new Vector3(-3.5f, 0, -8), Quaternion.Euler(0, 90, 0));
		NetworkServer.Spawn(insItem2);

		UpdateTankString();
	}

	public override void OnStartClient() { }

	public override void OnStopHost() { }

	public override void OnStopServer() { }

	public override void OnStopClient() { }

	#endregion
}
