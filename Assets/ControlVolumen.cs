using UnityEngine;
using UnityEngine.UI;

/*
GameObject: ControlVolumen (attach to UI element that manages audio volume)
Descripción: Sincroniza un Slider con el volumen de un AudioSource y aplica cambios.
*/

public class ControlVolumen : MonoBehaviour
{
    public AudioSource musicaFondo;
    public Slider sliderVolumen;

    // Start: inicializa el slider y registra el listener.
    void Start()
    {
        sliderVolumen.value = musicaFondo.volume;
        sliderVolumen.onValueChanged.AddListener(CambiarVolumen);
    }

    // CambiarVolumen: aplica el valor del slider al AudioSource.
    // Parámetros: valor - nuevo volumen (0..1).
    public void CambiarVolumen(float valor)
    {
        musicaFondo.volume = valor;
    }
}
