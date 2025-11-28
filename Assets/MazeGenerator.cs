using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
GameObject: MazeGenerator (attach to an empty GameObject in the level)
Descripción: Genera la rejilla de MazeCell, define entradas/salida, spawnea powerups y characters.
*/

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;
    [SerializeField] private GameObject _characterPrefab;
    [SerializeField] private CamaraTerceraPersona _cameraP1Controller;
    [SerializeField] private CamaraTerceraPersona _cameraP2Controller;

    [SerializeField] private GameObject _player1ModelPrefab;
    [SerializeField] private GameObject _player2ModelPrefab;

    [SerializeField] private GameObject _powerupPhasePrefab;
    [SerializeField] private GameObject _powerupTrueRadarPrefab;

    private MazeCell[,] _mazeGrid;                                                                      

    private Vector2Int _entryPosA = new Vector2Int(0, 0);
    private Vector2Int _entryPosB = new Vector2Int(0, 0);
    private Vector2Int _spawnPosA = new Vector2Int(0, 0);
    private Vector2Int _spawnPosB = new Vector2Int(0, 0);

    private Vector2Int _exitPos = new Vector2Int(0, 0);

    // Start: validaciones iniciales, instancia de celdas y generación completa.
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

        DefineEntranceAndExit();

        SpawnPowerups();

        SetupCharacters();
    }

    // GenerateMaze: genera recursivamente el laberinto marcando visitas y quitando muros.
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

    // Obtiene una celda adyacente no visitada aleatoria.
    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var unvisitedCells = GetUnvisitedCells(currentCell);
        return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
    }

    // Itera celdas adyacentes no visitadas.
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

    // ClearWalls: quita muros entre previousCell y currentCell según su posición relativa.
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

    // DefineEntranceAndExit: determina entradas/spawns en esquinas y crea ExitZone en la celda salida.
    private void DefineEntranceAndExit()
    {
        int axisChoice = Random.Range(0, 2);

        if (axisChoice == 0)
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
        else
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

        var exitCell = _mazeGrid[_exitPos.x, _exitPos.y];
        if (exitCell != null)
        {
            var exitGO = new GameObject("ExitZone");
            exitGO.transform.SetParent(exitCell.transform, false);
            exitGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var box = exitGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 1f, 0.9f);
            exitGO.AddComponent<ExitZone>();
            try { exitGO.tag = "Exit"; } catch (UnityException) { }
        }
    }

    // SetupCharacters: instancia prefabs de Character, asigna modelos, cámaras y manager.
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

        charA.Init(_mazeGrid, posA, "Jugador A");
        charB.Init(_mazeGrid, posB, "Jugador B");

        if (_cameraP1Controller != null) _cameraP1Controller.objetivo = goA.transform;
        if (_cameraP2Controller != null) _cameraP2Controller.objetivo = goB.transform;

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

    // SpawnPowerups: calcula posiciones válidas y spawnea powerups Phase y TrueRadar.
    private void SpawnPowerups()
    {
        int countEach = _mazeWidth / 4;

        var forbidden = new HashSet<Vector2Int>();
        forbidden.Add(_spawnPosA);
        forbidden.Add(_spawnPosB);
        forbidden.Add(_exitPos);

        var candidates = new List<Vector2Int>();
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                var pos = new Vector2Int(x, z);
                if (forbidden.Contains(pos)) continue;
                candidates.Add(pos);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            var tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        int idx = 0;
        for (int k = 0; k < countEach && idx < candidates.Count; k++, idx++)
        {
            var pos = candidates[idx];
            SpawnPowerupAt(pos, Powerup.PowerupType.Phase);
        }
        for (int k = 0; k < countEach && idx < candidates.Count; k++, idx++)
        {
            var pos = candidates[idx];
            SpawnPowerupAt(pos, Powerup.PowerupType.TrueRadar);
        }

        Debug.Log($"Spawned {countEach} Phase and {countEach} TrueRadar powerups (mazeWidth={_mazeWidth}, mazeDepth={_mazeDepth}).");
    }

    // SpawnPowerupAt: crea un powerup en la celda indicada.
    private void SpawnPowerupAt(Vector2Int cellPos, Powerup.PowerupType type)
    {
        var cell = _mazeGrid[cellPos.x, cellPos.y];
        if (cell == null) return;

        GameObject prefab = null;
        if (type == Powerup.PowerupType.Phase) prefab = _powerupPhasePrefab;
        else prefab = _powerupTrueRadarPrefab;

        GameObject go;
        float spawnY = 0.5f;
        if (prefab != null)
        {
            go = Instantiate(prefab, new Vector3(cellPos.x, spawnY, cellPos.y), Quaternion.identity);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = new Vector3(cellPos.x, spawnY, cellPos.y);
            go.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
        }

        var p = go.GetComponent<Powerup>();
        if (p == null) p = go.AddComponent<Powerup>();
        p.Type = (type == Powerup.PowerupType.Phase) ? Powerup.PowerupType.Phase : Powerup.PowerupType.TrueRadar;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (type == Powerup.PowerupType.Phase) mr.material.color = Color.cyan;
            else mr.material.color = Color.magenta;
        }

        go.transform.SetParent(cell.transform, true);

        var col = go.GetComponent<Collider>();
        if (col == null) { var bc = go.AddComponent<BoxCollider>(); bc.isTrigger = true; }
        else col.isTrigger = true;
    }
}