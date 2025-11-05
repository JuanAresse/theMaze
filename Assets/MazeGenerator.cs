using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private GameObject _characterPrefab; // Prefab con el componente Character
    [SerializeField] private CamaraTerceraPersona _cameraP1Controller;
    [SerializeField] private CamaraTerceraPersona _cameraP2Controller;

    // Añade aquí los modelos (asignar desde Inspector)
    [SerializeField] private GameObject _player1ModelPrefab;
    [SerializeField] private GameObject _player2ModelPrefab;

    private MazeCell[,] _mazeGrid;

    // Entrada exterior (esquinas) y spawn según petición
    private Vector2Int _entryPosA = new Vector2Int(0, 0);
    private Vector2Int _entryPosB = new Vector2Int(0, 0);
    private Vector2Int _spawnPosA = new Vector2Int(0, 0);
    private Vector2Int _spawnPosB = new Vector2Int(0, 0);

    // Única salida del laberinto
    private Vector2Int _exitPos = new Vector2Int(0, 0);

    void Start()
    {
        if (_mazeCellPrefab == null)
        {
            Debug.LogError("MazeGenerator: _mazeCellPrefab no asignado.");
            enabled = false;
            return;
        }

        if (_mazeWidth < 3 || _mazeDepth < 3)
        {
            Debug.LogError("MazeGenerator: _mazeWidth y _mazeDepth deben ser al menos 3.");
            enabled = false;
            return;
        }

        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(x, 0, z), Quaternion.identity);
            }
        }

        GenerateMaze(null, _mazeGrid[0, 0]);

        // Spawns exactos en esquinas y una única salida en la pared contraria
        DefineEntranceAndExit();

        SetupCharacters();
    }

    private void GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        MazeCell nextCell;
        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);
            if (nextCell != null) GenerateMaze(currentCell, nextCell);
        } while (nextCell != null);
    }

    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var unvisitedCells = GetUnvisitedCells(currentCell);
        return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
    }

    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
    {
        int x = (int)currentCell.transform.position.x;
        int z = (int)currentCell.transform.position.z;

        if (x + 1 < _mazeWidth)
        {
            var cellToRight = _mazeGrid[x + 1, z];
            if (!cellToRight.IsVisited) yield return cellToRight;
        }
        if (x - 1 >= 0)
        {
            var cellToLeft = _mazeGrid[x - 1, z];
            if (!cellToLeft.IsVisited) yield return cellToLeft;
        }
        if (z + 1 < _mazeDepth)
        {
            var cellToFront = _mazeGrid[x, z + 1];
            if (!cellToFront.IsVisited) yield return cellToFront;
        }
        if (z - 1 >= 0)
        {
            var cellToBack = _mazeGrid[x, z - 1];
            if (!cellToBack.IsVisited) yield return cellToBack;
        }
    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null) return;

        if (previousCell.transform.position.x < currentCell.transform.position.x)
        {
            previousCell.ClearRightWall();
            currentCell.ClearLeftWall();
            return;
        }
        if (previousCell.transform.position.x > currentCell.transform.position.x)
        {
            previousCell.ClearLeftWall();
            currentCell.ClearRightWall();
            return;
        }
        if (previousCell.transform.position.z < currentCell.transform.position.z)
        {
            previousCell.ClearFrontWall();
            currentCell.ClearBackWall();
            return;
        }
        if (previousCell.transform.position.z > currentCell.transform.position.z)
        {
            previousCell.ClearBackWall();
            currentCell.ClearFrontWall();
            return;
        }
    }

    /// Define las entradas en las esquinas (entryPos) y spawns en las esquinas.
    private void DefineEntranceAndExit()
    {
        int axisChoice = Random.Range(0, 2);

        if (axisChoice == 0) // izquierda/derecha
        {
            int entrySide = Random.Range(0, 2);
            if (entrySide == 0)
            {
                _entryPosA = new Vector2Int(0, 0);
                _entryPosB = new Vector2Int(0, _mazeDepth - 1);
                _spawnPosA = _entryPosA;
                _spawnPosB = _entryPosB;
                int exitZ = Random.Range(0, _mazeDepth);
                _exitPos = new Vector2Int(_mazeWidth - 1, exitZ);
                _mazeGrid[_exitPos.x, _exitPos.y].ClearRightWall();
            }
            else
            {
                _entryPosA = new Vector2Int(_mazeWidth - 1, 0);
                _entryPosB = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
                _spawnPosA = _entryPosA;
                _spawnPosB = _entryPosB;
                int exitZ = Random.Range(0, _mazeDepth);
                _exitPos = new Vector2Int(0, exitZ);
                _mazeGrid[_exitPos.x, _exitPos.y].ClearLeftWall();
            }
        }
        else // inferior/superior
        {
            int entrySide = Random.Range(0, 2);
            if (entrySide == 0)
            {
                _entryPosA = new Vector2Int(0, 0);
                _entryPosB = new Vector2Int(_mazeWidth - 1, 0);
                _spawnPosA = _entryPosA;
                _spawnPosB = _entryPosB;
                int exitX = Random.Range(0, _mazeWidth);
                _exitPos = new Vector2Int(exitX, _mazeDepth - 1);
                _mazeGrid[_exitPos.x, _exitPos.y].ClearFrontWall();
            }
            else
            {
                _entryPosA = new Vector2Int(0, _mazeDepth - 1);
                _entryPosB = new Vector2Int(_mazeWidth - 1, _mazeDepth - 1);
                _spawnPosA = _entryPosA;
                _spawnPosB = _entryPosB;
                int exitX = Random.Range(0, _mazeWidth);
                _exitPos = new Vector2Int(exitX, 0);
                _mazeGrid[_exitPos.x, _exitPos.y].ClearBackWall();
            }
        }

        Debug.Log($"Entradas (exteriores): A={_entryPosA} B={_entryPosB}  Spawns: A={_spawnPosA} B={_spawnPosB}  Salida: {_exitPos}");

        // Crear una zona trigger dentro de la celda de salida para detectar la salida cuando el personaje
        // entre en esa celda. No dependemos únicamente de tags para evitar errores si la tag no existe.
        var exitCell = _mazeGrid[_exitPos.x, _exitPos.y];
        if (exitCell != null)
        {
            var exitGO = new GameObject("ExitZone");
            exitGO.transform.SetParent(exitCell.transform, false);
            // Centrado en la celda, altura 0.5 (ajusta si tu collider del personaje está a diferente Y)
            exitGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var box = exitGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // Tamaño ligeramente menor que la celda para evitar colisiones con muros
            box.size = new Vector3(0.9f, 1f, 0.9f);
            exitGO.AddComponent<ExitZone>();
            // Intentar asignar la tag "Exit" si existe (no obligatorio)
            try { exitGO.tag = "Exit"; } catch (UnityException) { /* tag no definida: OK */ }
        }
    }

    private void SetupCharacters()
    {
        if (_characterPrefab == null)
        {
            Debug.LogWarning("Character prefab no asignado en MazeGenerator.");
            return;
        }

        Vector2Int posA = _spawnPosA;
        Vector2Int posB = _spawnPosB;

        var goA = Instantiate(_characterPrefab, new Vector3(posA.x, 0.5f, posA.y), Quaternion.identity);
        var goB = Instantiate(_characterPrefab, new Vector3(posB.x, 0.5f, posB.y), Quaternion.identity);

        var charA = goA.GetComponent<Character>();
        var charB = goB.GetComponent<Character>();

        if (charA == null || charB == null)
        {
            Debug.LogError("Prefabs deben tener el componente Character.");
            return;
        }

        // Instanciar modelos si están asignados y adjuntar como visual al Character
        if (_player1ModelPrefab != null)
        {
            var modelA = Instantiate(_player1ModelPrefab, goA.transform, false);
            charA.AttachVisual(modelA);
        }
        if (_player2ModelPrefab != null)
        {
            var modelB = Instantiate(_player2ModelPrefab, goB.transform, false);
            charB.AttachVisual(modelB);
        }

        // Inicializar characters con la rejilla y posición BEFORE manager.Initialize
        charA.Init(_mazeGrid, posA, "Jugador A");
        charB.Init(_mazeGrid, posB, "Jugador B");

        // Asignar objetivo a controladores de cámara (si están) para centrar cámaras en los personajes
        if (_cameraP1Controller != null) _cameraP1Controller.objetivo = goA.transform;
        if (_cameraP2Controller != null) _cameraP2Controller.objetivo = goB.transform;

        // Determinar cámaras a usar para el manager
        Camera camA = null;
        Camera camB = null;
        if (_cameraP1Controller != null)
        {
            camA = _cameraP1Controller.GetComponent<Camera>() ?? _cameraP1Controller.GetComponentInChildren<Camera>();
        }
        if (_cameraP2Controller != null)
        {
            camB = _cameraP2Controller.GetComponent<Camera>() ?? _cameraP2Controller.GetComponentInChildren<Camera>();
        }

        // Fallback robusto a Camera.allCameras
        var cams = Camera.allCameras;
        if (camA == null || camB == null)
        {
            if (cams.Length >= 2)
            {
                if (camA == null) camA = cams[0];
                if (camB == null) camB = cams[1];
            }
            else if (cams.Length == 1)
            {
                if (camA == null) camA = cams[0];
                if (camB == null) camB = cams[0];
            }
            else
            {
                camA = Camera.main;
                camB = Camera.main;
            }
        }

        Debug.Log($"Selected cameras: camA={(camA!=null?camA.name:"null")} camB={(camB!=null?camB.name:"null")}");
        if (camA == camB) Debug.LogWarning("camA and camB are the same Camera. Assign two different cameras for split-screen.");

        // --- CONFIGURACIÓN SEGURA DE CÁMARAS PARA SPLIT-SCREEN ---
        // Si tenemos dos cámaras distintas, forzamos rects left/right.
        if (camA != null && camB != null && camA != camB)
        {
            camA.enabled = true;
            camB.enabled = true;

            camA.rect = new Rect(0f, 0f, 0.5f, 1f);
            camB.rect = new Rect(0.5f, 0f, 0.5f, 1f);

            camA.clearFlags = CameraClearFlags.Skybox;
            camB.clearFlags = CameraClearFlags.Skybox;

            camA.backgroundColor = new Color(0.6f, 0.65f, 0.7f);
            camB.backgroundColor = new Color(0.6f, 0.65f, 0.7f);

            camA.cullingMask = ~0; // all layers
            camB.cullingMask = ~0;

            // Depths: keep same depth, canvases will render with their own sorting orders.
            camA.depth = 0;
            camB.depth = 0;
        }
        else if (camA != null) // single camera fallback
        {
            camA.enabled = true;
            camA.rect = new Rect(0f, 0f, 1f, 1f);
            camA.clearFlags = CameraClearFlags.Skybox;
            camA.backgroundColor = new Color(0.6f, 0.65f, 0.7f);
            camA.cullingMask = ~0;
            camA.depth = 0;
        }

        // Obtener o crear el TurnBasedManager y usarlo para crear UIs y controlar turnos
        var manager = gameObject.GetComponent<TurnBasedManager>();
        if (manager == null) manager = gameObject.AddComponent<TurnBasedManager>();

        // Pasar las cámaras explícitamente para que el manager cree los canvases ligados a cada cámara
        manager.Initialize(charA, charB, camA, camB);

        // Asignar referencia al manager en los personajes
        charA.manager = manager;
        charB.manager = manager;

        Debug.Log($"Personajes configurados: {charA.name} en {posA} , {charB.name} en {posB}");
    }
}