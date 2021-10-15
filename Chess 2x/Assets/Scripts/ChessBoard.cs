using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class ChessBoard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;

    [Header("Prefabs and Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    // LOGIC
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private SpecialMove specialMove;
    private GameObject[,] tiles;
    public Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    public bool isWhiteTurn;
    public int whiteTurn = 1;
    public int blackTurn;
    public bool inCheck = false;

    private void Awake()
    {
        isWhiteTurn = true;
        whiteTurn = 0;
        blackTurn = 0;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();
    }

    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            //get indexes of tile we hit
            Vector2Int hitPosition = LookUpTileIndex(info.transform.gameObject);

            ///if we were already hovering, change previous tile
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile"); ;
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //hovering over tile after not hovering over any tiles
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //If we press down on mouse
            if (Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    //Is it our turn
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && whiteTurn < 2) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && blackTurn < 2))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        // Get a list of where we can go, highlight those tiles
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        // Get a list of special moves
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }
            //If we release mouse
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);
                if (!validMove)
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                }

                currentlyDragging = null;
                RemoveTiles();
            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }
             
            if(currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveTiles();
            }
        }

        //If we are dragging a piece
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }

    // Generate the Board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }
    
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) + bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) + bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) + bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize ) + bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");

        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    // Spawning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0, blackTeam = 1;

        // White
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        // Black
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);      
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    // Positioning
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
    }
    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize);
    }

    //Highlight Tiles
    public void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    public void RemoveTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }

    // Checkmate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        if (winningTeam == 1)
        {
            victoryScreen.transform.GetChild(1).gameObject.SetActive(true);
            victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        }
        if(winningTeam == 0)
        {
            victoryScreen.transform.GetChild(0).gameObject.SetActive(true);
            victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        }
    }
    public void OnResetButton()
    {
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);
                chessPieces[x, y] = null;
            }
        }

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
        blackTurn = 0;
        whiteTurn = 0;
    }
    public void ExitButton()
    {
        Application.Quit();
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];

            if(myPawn.currentX == enemyPawn.currentX)
            {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                    Destroy(enemyPawn.gameObject);
                }
            }
            
        }

        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn)
            {
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                if (targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            var lastMove = moveList[moveList.Count - 1];

            // Left
            if(lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0){ // White
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if(lastMove[1].y == 7){ // Black
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            // Right
            else if(lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0){ // White
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                { // Black
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }       
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(chessPieces[x, y] != null)
                if (chessPieces[x, y].type == ChessPieceType.King)
                    if (chessPieces[x, y].team == currentlyDragging.team)
                        targetKing = chessPieces[x, y];
        // Since we are sending in ref availableMoves, we will be deleting moves that will put us in check
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        // Save current value, reset after function call
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        // SIMULATION OF PIECES
        // Going through all moves, simulate, and see if we are in check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            // Did we simulate king move
            if(cp.type == ChessPieceType.King)
                kingPositionSim = new Vector2Int(simX, simY);

            // Copy the [,] NOT REFERENCE
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simulationAttacking = new List<ChessPiece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simulationAttacking.Add(simulation[x, y]);
                    }
                }
            }

            // Simulate the moves
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // Did one of the pieces get taken during sim 
            var deadPiece = simulationAttacking.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
                simulationAttacking.Remove(deadPiece);

            // Get all simulated attacking pieces moves
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simulationAttacking.Count; a++)
            {
                var pieceMoves = simulationAttacking[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }

            // Is King in trouble, if it is remove that move
            if(ContainsValidMove(ref simMoves, kingPositionSim))
            {
                movesToRemove.Add(moves[i]);
            }

            // Restore the chess piece datea
            cp.currentX = actualX;
            cp.currentY = actualY;
        }

        // Remove from the current available move list
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
    }
    private bool CheckForCheckmate()
    {
        inCheck = false;
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPiece = new List<ChessPiece>();
        List<ChessPiece> defendingPiece = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPiece.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];
                    }
                    else
                    {
                        attackingPiece.Add(chessPieces[x, y]);
                    }
                }

        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPiece.Count; i++)
        {
            var pieceMoves = attackingPiece[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailableMoves.Add(pieceMoves[b]);
            }
        }

        // Are we in check 
        if(ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            print("We are in check");
            print("It is white's turn: " + isWhiteTurn);
            inCheck = true;
            if (isWhiteTurn)
            {
                isWhiteTurn = !isWhiteTurn;
                whiteTurn = 2;
                blackTurn = 0;             
            }

            else if (!isWhiteTurn)
            {
                isWhiteTurn = true;
                whiteTurn = 0;
                blackTurn = 2;
            }

            // King is attacked, can something block
            for (int i = 0; i < defendingPiece.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPiece[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMoveForSinglePiece(defendingPiece[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                {
                    return false;
                }
            }
            return true; // Checkmate
        }
        return false;
    }

    // Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        }
        return false;
    }
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if(!ContainsValidMove(ref availableMoves, new Vector2Int(x,y)))
            return false;
        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);
        
        // Is there another piece on the target position
        if(chessPieces[x,y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];
            if(cp.team == ocp.team)
            {
                return false;
            }

            // If its the enemy team
            if(ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);
                Destroy(ocp.gameObject);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);

                Destroy(ocp.gameObject);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);

        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });

        ProcessSpecialMove();
        if (CheckForCheckmate())
        {
           print("CM");
           CheckMate(cp.team);
        }
        else if(!inCheck)
        {
            if (isWhiteTurn)
            {
                if (whiteTurn < 2)
                    whiteTurn++;
                if (whiteTurn >= 2)
                {
                    isWhiteTurn = !isWhiteTurn;
                    blackTurn = 0;
                }
            }

            else if (!isWhiteTurn)
            {
                if (blackTurn < 2)
                    blackTurn++;
                if (blackTurn >= 2)
                {
                    isWhiteTurn = true;
                    whiteTurn = 0;
                }
            }
        }

        return true;
    }
    private Vector2Int LookUpTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
        return -Vector2Int.one; // Invalid
    }
}
