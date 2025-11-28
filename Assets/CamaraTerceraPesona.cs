using UnityEngine;

/*
GameObject: CamaraTerceraPersona (attach to camera GameObject)
Descripción: Sigue a un objetivo (Transform) con un offset diagonal y suavizado.
*/

public class CamaraTerceraPersona : MonoBehaviour
{
    public Transform objetivo;
    public Vector3 offsetDiagonal = new Vector3(3f, 2f, -5f);
    public float velocidadSuavizado = 5f;

    // LateUpdate: calcula posición suavizada y orienta la cámara hacia el objetivo.
    void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        Vector3 posicionDeseada = objetivo.position + offsetDiagonal;

        Vector3 posicionSuavizada = Vector3.Lerp(transform.position, posicionDeseada, velocidadSuavizado * Time.deltaTime);

        transform.position = posicionSuavizada;

        transform.LookAt(objetivo);
    }
}