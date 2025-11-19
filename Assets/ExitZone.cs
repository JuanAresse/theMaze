using UnityEngine;

// Componente marcador para la zona de salida.
// Ahora con fallback por trigger para notificar al TurnBasedManager.
public class ExitZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Intentar localizar un Character en el collider entrante
        Character c = other.GetComponent<Character>() ?? other.GetComponentInParent<Character>() ?? other.GetComponentInChildren<Character>();
        if (c == null) return;

        Debug.Log($"[ExitZone] Detected Character '{c.name}' entering exit trigger. Notifying manager.");

        // Preferir el manager referencia del propio Character (si existe), sino buscar uno en escena
        if (c.manager != null)
        {
            c.manager.PlayerExited(c);
            return;
        }

        var mgr = UnityEngine.Object.FindObjectOfType<TurnBasedManager>();
        if (mgr != null) mgr.PlayerExited(c);
        else Debug.LogWarning("[ExitZone] No TurnBasedManager encontrado para notificar salida.");
    }
}