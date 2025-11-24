using UnityEngine;

/*
GameObject: Powerup (attach to powerup prefab/visual)
Descripción: Maneja la interacción de recogida del powerup y notifica al Character y manager.
*/

public class Powerup : MonoBehaviour
{
    public enum PowerupType { Phase, TrueRadar }

    public PowerupType Type = PowerupType.Phase;
    public float VisualHeight = 0.1f;

    // Reset: asegura que exista un collider trigger en edición.
    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }
    }

    // Awake: asegura collider trigger en runtime.
    private void Awake()
    {
        var c = GetComponent<Collider>();
        if (c == null)
        {
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
        }
        else
        {
            c.isTrigger = true;
        }
    }

    // OnTriggerEnter: si un Character entra, lo notifica, muestra UI y destruye el objeto.
    private void OnTriggerEnter(Collider other)
    {
        var character = other.GetComponentInParent<Character>();
        if (character == null) return;

        character.CollectPowerup(Type);

        if (character.manager != null)
        {
            character.manager.ShowPowerupCollected(character, Type);
        }

        Destroy(gameObject);
    }
}