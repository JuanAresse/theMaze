using UnityEngine;

public class CamaraTerceraPersona : MonoBehaviour
{
    // Objeto que la cámara seguirá y mirará
    public Transform objetivo;

    // Distancia y ángulo diagonal respecto al objetivo
    public Vector3 offsetDiagonal = new Vector3(3f, 2f, -5f);

    // Velocidad de seguimiento para que la cámara no sea brusca
    public float velocidadSuavizado = 5f;

    void LateUpdate()
    {
        // 1. Verificar si hay un objetivo (jugador)
        if (objetivo == null)
        {
            // Opcional: buscar el objetivo si aún no está asignado
            return;
        }

        // 2. Calcular la posición deseada de la cámara
        // La posición deseada es la posición del objetivo más el offset diagonal
        Vector3 posicionDeseada = objetivo.position + offsetDiagonal;

        // 3. Suavizar el movimiento de la cámara
        // Movemos la cámara desde su posición actual hacia la posición deseada
        Vector3 posicionSuavizada = Vector3.Lerp(transform.position, posicionDeseada, velocidadSuavizado * Time.deltaTime);

        // 4. Aplicar la posición
        transform.position = posicionSuavizada;

        // 5. Mirar al objetivo
        // Hacemos que la cámara mire directamente al centro del jugador
        transform.LookAt(objetivo);
    }
}