using UnityEngine;

/*
GameObject: ExitZone (attach to a trigger inside a MazeCell)
Descripción: Detecta la entrada de un Character y notifica al TurnBasedManager la salida.
*/

public class ExitZone : MonoBehaviour
{
    // OnTriggerEnter: detecta Character que entra y notifica al manager.
    private void OnTriggerEnter(Collider other)
    {
        Character c = other.GetComponent<Character>() ?? other.GetComponentInParent<Character>() ?? other.GetComponentInChildren<Character>();
        if (c == null) return;

        Debug.Log($"[ExitZone] Detected Character '{c.name}' entering exit trigger. Notifying manager.");

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