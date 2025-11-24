using System.Collections;
using UnityEngine;

/*
GameObject: CameraController (attach to Camera)
Descripción: Centra y ajusta el zoom/posición de la cámara para cubrir todas las MazeCell de la escena.
*/

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Tooltip("Padding en unidades del mundo alrededor del laberinto.")]
    public float Padding = 1f;

    [Tooltip("Forzar cámara ortográfica.")]
    public bool ForceOrthographic = false;

    [Tooltip("Mover suavemente.")]
    public bool Smooth = false;

    [Tooltip("Tiempo de suavizado (segundos).")]
    public float SmoothTime = 0.25f;

    [Tooltip("Altura mínima para cámara en perspectiva.")]
    public float MinHeight = 5f;

    [Tooltip("Altura máxima para cámara en perspectiva.")]
    public float MaxHeight = 200f;

    [Tooltip("Tiempo máximo a esperar a que se generen las celdas (segundos).")]
    public float WaitTimeout = 2f;

    Camera _cam;
    Vector3 _posVelocity;
    float _sizeVelocity;

    // Start: inicializa componentes y espera la generación de MazeCell antes de centrar.
    IEnumerator Start()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogWarning("CameraController: falta componente Camera.");
            yield break;
        }

        if (ForceOrthographic) _cam.orthographic = true;

        float waited = 0f;
#if UNITY_2023_2_OR_NEWER
        while (Object.FindObjectsByType<MazeCell>(FindObjectsSortMode.None).Length == 0 && waited < WaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }
#else
        while (FindObjectsOfType<MazeCell>().Length == 0 && waited < WaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }
#endif

        CenterAndZoom();
    }

    // CenterAndZoom: calcula el bounding box de MazeCell y ajusta posición/tamaño de cámara.
    public void CenterAndZoom()
    {
#if UNITY_2023_2_OR_NEWER
        var cells = Object.FindObjectsByType<MazeCell>(FindObjectsSortMode.None);
#else
        var cells = FindObjectsOfType<MazeCell>();
#endif
        if (cells == null || cells.Length == 0)
        {
            Debug.LogWarning("CameraController: no se encontraron MazeCell para centrar.");
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var c in cells)
        {
            Vector3 p = c.transform.position;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        float widthWorld = Mathf.Max(1f, maxX - minX + 1f);
        float heightWorld = Mathf.Max(1f, maxZ - minZ + 1f);

        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

        if (_cam.orthographic || ForceOrthographic)
        {
            float halfHeight = heightWorld * 0.5f + Padding;
            float halfWidth = widthWorld * 0.5f + Padding;
            float requiredSize = Mathf.Max(halfHeight, halfWidth / _cam.aspect);

            Vector3 targetPos = new Vector3(center.x, _cam.transform.position.y, center.z);
            if (Smooth)
            {
                _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, requiredSize, ref _sizeVelocity, SmoothTime);
                _cam.transform.position = Vector3.SmoothDamp(_cam.transform.position, targetPos, ref _posVelocity, SmoothTime);
            }
            else
            {
                _cam.orthographicSize = requiredSize;
                _cam.transform.position = targetPos;
            }

            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            float verticalHalfFovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHeight = heightWorld * 0.5f + Padding;
            float halfWidth = widthWorld * 0.5f + Padding;

            float d_v = halfHeight / Mathf.Tan(verticalHalfFovRad);
            float horizontalHalfFovRad = Mathf.Atan(Mathf.Tan(verticalHalfFovRad) * _cam.aspect);
            float d_h = halfWidth / Mathf.Tan(horizontalHalfFovRad);

            float distance = Mathf.Max(d_v, d_h);
            distance = Mathf.Clamp(distance, MinHeight, MaxHeight);

            Vector3 targetPos = new Vector3(center.x, distance, center.z);
            if (Smooth)
                _cam.transform.position = Vector3.SmoothDamp(_cam.transform.position, targetPos, ref _posVelocity, SmoothTime);
            else
                _cam.transform.position = targetPos;

            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}

// Ejecuta este script en Play para listar cámaras con TargetTexture no-nulo o con tamaño 0.
public class LogCameraTargets : MonoBehaviour
{
    void Start()
    {
        foreach (var cam in Camera.allCameras)
        {
            var rt = cam.targetTexture;
            if (rt == null)
            {
                Debug.Log($"Camera '{cam.name}' targetTexture: null");
            }
            else
            {
                Debug.Log($"Camera '{cam.name}' targetTexture: {rt.name} size {rt.width}x{rt.height} format {rt.format}");
                if (rt.width == 0 || rt.height == 0)
                    Debug.LogWarning($"Camera '{cam.name}' tiene RenderTexture con tamaño 0!");
            }
        }
    }
}