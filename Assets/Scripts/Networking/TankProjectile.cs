using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TankProjectile : NetworkBehaviour {
	[HideInInspector]
	public TankController sender = null;

	private void Start() {
		if (isServer) StartCoroutine(TimedDestruction());
	}
	private void Update() {
		if (isClientOnly) return;

		transform.Translate(Time.deltaTime * 25 * Vector3.forward);

		foreach (Collider c in Physics.OverlapSphere(transform.position, 0.2f,
			LayerMask.GetMask("Tank Armor"))) {

			if (c.transform.parent.TryGetComponent(out TankController t)) {
				if (t == sender) continue;

				t.TankHit(transform.position, sender, 100, true);
				NetworkServer.Destroy(gameObject);
				break;
			}
		}
	}
	private IEnumerator TimedDestruction() {
		yield return new WaitForSeconds(2);
		NetworkServer.Destroy(gameObject);
	}
}
