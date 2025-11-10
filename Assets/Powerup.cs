using UnityEngine;

public class Powerup : MonoBehaviour
{
    public enum PowerupType { Phase, TrueRadar }

    public PowerupType Type = PowerupType.Phase;
    [Tooltip("Altura local donde se colocará el visual (si aplica)")]
    public float VisualHeight = 0.1f;

    private void Reset()
    {
        // Asegurar que hay un collider trigger
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

    private void Awake()
    {
        // Garantizar collider trigger en runtime
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

    private void OnTriggerEnter(Collider other)
    {
        // Buscar componente Character en el objeto que activó el trigger
        var character = other.GetComponentInParent<Character>();
        if (character == null) return;

        // Notificar al Character
        character.CollectPowerup(Type);

        // Mostrar notificación en pantalla via el manager (si existe)
        if (character.manager != null)
        {
            character.manager.ShowPowerupCollected(character, Type);
        }

        // Destruir el powerup para que desaparezca inmediatamente
        Destroy(gameObject);
    }
}