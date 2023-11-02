using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TankProjectile : NetworkBehaviour {
	[HideInInspector]
	public TankController sender = null;

	[SerializeField]
	private GameObject explosionPrefab; //spawned locally via RPC

	private bool destroyed = false; //don't call destruction twice in same frame

	private void OnTriggerEnter(Collider other) {
		if (isServer && !destroyed) {
			if (other.gameObject.layer == LayerMask.NameToLayer("Tank Armor")) {
				if (other.transform.parent.TryGetComponent(out TankController t) && t != sender) {
					t.TankHit(transform.position, sender, 350, true);
					StartCoroutine(DelayedDestroy());
				}
			} else if (other.gameObject.layer == LayerMask.NameToLayer("Default")) {
				StartCoroutine(DelayedDestroy());
			}
		}

	}
	[Server]
	IEnumerator DelayedDestroy() {
		destroyed = true;
		SpawnExplosion();

		yield return new WaitForSeconds(1f);
		NetworkServer.Destroy(gameObject);
	}
	[ClientRpc]
	private void SpawnExplosion() {
		print("explode");
		GetComponent<MeshRenderer>().enabled = false;
		GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
		Destroy(explosion, 5f);
	}

	private void Start() {
		if (isServer) StartCoroutine(TimedDestruction());
	}
	private void Update() {
		transform.Translate(Time.deltaTime * 25 * Vector3.forward);
	}
	private IEnumerator TimedDestruction() {
		yield return new WaitForSeconds(5);
		NetworkServer.Destroy(gameObject);
	}
}
