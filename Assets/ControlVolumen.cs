using UnityEngine;
using UnityEngine.UI;

public class ControlVolumen : MonoBehaviour
{
    public AudioSource musicaFondo;
    public Slider sliderVolumen;

    void Start()
    {
        // Si querés que el slider arranque con el volumen actual de la música
        sliderVolumen.value = musicaFondo.volume;

        // Cuando cambie el valor del slider, llamará al método CambiarVolumen
        sliderVolumen.onValueChanged.AddListener(CambiarVolumen);
    }

    public void CambiarVolumen(float valor)
    {
        musicaFondo.volume = valor;
    }
}
