using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace EdGames
{
	public class BubblesEdGame : EditorWindow
	{
		private const int GridWidth = 19;
		private const int GridHeight = 16;
		private const int CellSize = 21;
		private const int CellSizeHalf = CellSize / 2;

		private const float rowsTimeout = 30f;
		private const float bubbleMoveSpeed = 2f;

		private static readonly GUIContent GC_Help = new GUIContent("?");
		private static readonly GUIContent GC_Reset = new GUIContent("Reset");
		private static readonly GUIContent GC_Label = new GUIContent("Bubbles Game");
		private static readonly GUIContent[] GC_HelpLines =
			{
				new GUIContent("move mouse to aim"),
				new GUIContent("left-click to shoot"),
			};

		private static readonly Color[] BubbleColours = 
			{
				new Color(1.0f, 1.0f, 0.0f, 1f),
				new Color(1.0f, 0.0f, 0.0f, 1f),
				new Color(0.0f, 1.0f, 1.0f, 1f),
				new Color(1.0f, 0.5f, 0.0f, 1f),
				new Color(0.0f, 0.0f, 1.0f, 1f),
				new Color(1.0f, 0.0f, 1.0f, 1f),
				new Color(0.0f, 1.0f, 0.0f, 1f)
			};

		public class ScoreInfo
		{
			public Rect pos;
			public GUIContent label;
			public float timer;
		}

		private Texture2D[] textures;
		private GUIStyle backStyle;
		private GUIStyle scoreStyle;

		private bool doRepaint = false;
		private Rect boardRect;
		private Rect clickAreaRect;
		private Rect cellRect;
		private Rect labelRect;
		private Rect helpButtonRect;
		private Rect resetButtonRect;
		private Vector2 pivotPoint;

		[System.NonSerialized] private int bubbleType = -1;
		private int[] grid = new int[GridWidth * GridHeight];
		private Vector2 bubblePos;
		private Vector2 moveDir;
		private double time;
		private bool gameActive;
		private bool playerControlActive;
		private bool bubbleMoving;
		private int score;
		private Vector3 lastMousePos;
		private float jitter;
		private float jitterTime;
		private float rowsTimer;
		private List<ScoreInfo> floatingScores = new List<ScoreInfo>();

		// ------------------------------------------------------------------------------------------------------------------

		[MenuItem("Window/Games/Bubbles Game")]
		private static void OpenBubblesEdGame()
		{
			GetWindow<BubblesEdGame>("Bubbles Game");
		}

		private void Awake()
		{
			boardRect = new Rect(5, (CellSize * 2) + 5, (CellSize * GridWidth) + 2 + CellSizeHalf, (CellSize * GridHeight) + 2);
			clickAreaRect = new Rect(5, (CellSize * 2) + 5, (CellSize * GridWidth) + 2 + CellSizeHalf, (CellSize * (GridHeight - 2)) + 2);
			cellRect = new Rect(0, 0, CellSize, CellSize);
			labelRect = new Rect(5, 10, boardRect.width, 20);
			helpButtonRect = new Rect(boardRect.xMax - 20, 13, 20, 15);
			resetButtonRect = new Rect(helpButtonRect.x - 50, 13, 50, 15);
			pivotPoint = new Vector2(boardRect.center.x, boardRect.yMax);
			lastMousePos = pivotPoint + new Vector2(0, -100f);
			minSize = new Vector2(boardRect.width + 10, boardRect.height + (CellSize * 3) + 10);

			GenerateAssets(); 
			ResetGame();
		}

		private void OnDestroy()
		{
			DestroyAssets();
		}

		private void OnFocus()
		{
			time = EditorApplication.timeSinceStartup;
		}

		private void OnGUI()
		{
			Event ev = Event.current;

			if (bubbleType < 0)
			{
				ResetGame();
			}

			if (gameActive && focusedWindow == this)
			{
				doRepaint = true;
				DoUpdate(ev);
			}

			DoDraw(ev);

			if (doRepaint)
			{
				doRepaint = false;
				Repaint();
			}
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void DoUpdate(Event ev)
		{
			float deltatime = (float)(EditorApplication.timeSinceStartup - time);
			time = EditorApplication.timeSinceStartup;

			// handle input
			if (playerControlActive && ev.type == EventType.MouseDown && clickAreaRect.Contains(ev.mousePosition))
			{
				playerControlActive = false; // no player input allowed until bubble done moving
				bubbleMoving = true;
				moveDir = ev.mousePosition - bubblePos;
			}

			// handle bubble movement
			if (bubbleMoving)
			{
				MoveBubble(deltatime);
			}

			// advance rows
			rowsTimer -= deltatime;
			if (rowsTimer <= 0.0f)
			{
				rowsTimer = rowsTimeout;
				jitter = 0f;
				SelectBubble(bubbleType); // reset bubble so it don't cause problems with rows which moved down
				GenerateNewLine();
			}

			if (rowsTimer <= 1.5f)
			{
				jitterTime -= deltatime;
				if (jitterTime <= 0.0f)
				{
					jitterTime = 0.1f;
					jitter = (jitter <= 0.0f ? 0.5f : -0.5f);
				}
			}

			// update floating scores
			if (floatingScores.Count > 0)
			{
				doRepaint = true;
				for (int i = floatingScores.Count - 1; i >= 0; i--)
				{
					floatingScores[i].timer -= deltatime;
					floatingScores[i].pos.y -= deltatime * Random.Range(3f, 5f);
					if (floatingScores[i].timer <= 0.0f) floatingScores.RemoveAt(i);
				}
			}
		}

		private void MoveBubble(float dt)
		{
			bubblePos += moveDir * dt * bubbleMoveSpeed;

			// check if should stick to other bubbles
			int x, y;

			// this is buggy. sometimes a bubble will stop in the air cause not checking for the offset.
			Vector2 p = bubblePos;
			p = p + (moveDir.normalized * CellSizeHalf);

			EdGameUtil.CalcCellPos(boardRect, p, CellSize, true, out x, out y);
			if (y >= 0 && y < GridHeight && grid[y * GridWidth + x] >= 0)
			{
				EdGameUtil.CalcCellPos(boardRect, bubblePos, CellSize, true, out x, out y);
				if (y >= GridHeight || y < 0)
				{
					gameActive = false;
					return;
				}

				if (grid[y * GridWidth + x] < 0)
				{
					grid[y * GridWidth + x] = bubbleType;
					CheckAndBreakCells(x, y, bubbleType);
					SelectBubble();
					return;
				}
			}

			// bounce bubble off left/right edge
			if (bubblePos.x - CellSizeHalf < boardRect.x)
			{
				bubblePos.x = boardRect.x + CellSizeHalf + 1;
				moveDir.x *= -1;
			}
			else if (bubblePos.x + CellSizeHalf > boardRect.xMax)
			{
				bubblePos.x = boardRect.xMax - (CellSizeHalf + 1);
				moveDir.x *= -1;
			}

			// stick to top edge
			if (bubblePos.y - CellSizeHalf < boardRect.y)
			{
				bubblePos.y = boardRect.y + CellSizeHalf + 1;
				EdGameUtil.CalcCellPos(boardRect, bubblePos, CellSize, true, out x, out y);
				if (grid[y * GridWidth + x] < 0)
				{
					grid[y * GridWidth + x] = bubbleType;
					CheckAndBreakCells(x, y, bubbleType);
				}
				SelectBubble();
			}

		}

		private void ResetGame()
		{
			time = EditorApplication.timeSinceStartup;
			rowsTimer = rowsTimeout;
			gameActive = true;
			score = 0;
			for (int i = 0; i < grid.Length; i++) grid[i] = -1;
			for (int i = 0; i < 6; i++) GenerateNewLine(i);
			SelectBubble();
		}

		private void SelectBubble(int forceType = -1)
		{
			bubbleMoving = false;
			playerControlActive = true;
			bubbleType = forceType < 0 ? Random.Range(0, BubbleColours.Length) : forceType;
			bubblePos = pivotPoint;
		}

		private void GenerateNewLine(int row = -1)
		{
			if (row < 0)
			{   // need to shift everything down
				row = 0;

				if (MoveRowsDown())
				{	// game-over when a row with bubbles will go below the bottom 
					gameActive = false;
					return;
				}
			}

			for (int i = 0; i < GridWidth; i++)
			{
				grid[row * GridWidth + i] = Random.Range(0, BubbleColours.Length);
			}
		}

		private bool MoveRowsDown()
		{   // returns true if game-over (bubbles goes outside board at bottom)

			for (int y = GridHeight; y > 0; y--)
			{
				for (int x = 0; x < GridWidth; x++)
				{
					int b = grid[(y - 1) * GridWidth + x];
					if (b >= 0)
					{
						if (y == GridHeight) return true;
						grid[y * GridWidth + x] = b;
						grid[(y - 1) * GridWidth + x] = -1;
					}
				}
			}

			return false;
		}

		private void CheckAndBreakCells(int sx, int sy, int bubbleType)
		{
			// check if 3 or more of same colour are touching the newly placed cell
			int ox, oy;
			List<int> collected = new List<int>();
			AddCellToList(collected, sx, sy, bubbleType);

			if (collected.Count >= 3)
			{
				foreach (int i in collected)
				{
					grid[i] = -1;
					EdGameUtil.IndexToXY(GridWidth, i, out ox, out oy);
					IncScore(1, new Vector2(ox, oy));
				}

				// collect all cells attached to walls and ceiling or cells which are
				int idx;
				for (int x = 0; x < GridWidth; x++)
				{
					idx = GridWidth + x;
					if (grid[idx] >= 0) AddCellToList(collected, x, 0);
				}

				for (int y = 0; y < GridHeight; y++)
				{
					idx = y * GridWidth;
					if (grid[idx] >= 0) AddCellToList(collected, 0, y);

					idx = y * GridWidth + (GridWidth - 1);
					if (grid[idx] >= 0) AddCellToList(collected, GridWidth - 1, y);
				}

				// now run through grid and check what was not collected and remove it
				for (int i = 0; i < grid.Length; i++)
				{
					if (grid[i] >= 0 && !collected.Contains(i))
					{
						grid[i] = -1;
						EdGameUtil.IndexToXY(GridWidth, i, out ox, out oy);
						IncScore(2, new Vector2(ox, oy));
					}
				}

			}
		}

		private void AddCellToList(List<int> collected, int x, int y, int bubbleType)
		{
			int idx = y * GridWidth + x;
			if (collected.Contains(idx)) return;
			collected.Add(idx);

			// check if connected left
			if (x > 0 && grid[(x - 1) + y * GridWidth] == bubbleType) AddCellToList(collected, x - 1, y, bubbleType);

			// check if connected right
			if (x < GridWidth - 1 && grid[(x + 1) + y * GridWidth] == bubbleType) AddCellToList(collected, x + 1, y, bubbleType);

			// check if connected below
			if (y < GridHeight - 1 && grid[x + (y + 1) * GridWidth] == bubbleType) AddCellToList(collected, x, y + 1, bubbleType);

			// check if connected above
			if (y > 0 && grid[x + (y - 1) * GridWidth] == bubbleType) AddCellToList(collected, x, y - 1, bubbleType);

			// for above and below an extra cell to left or right must be checked depending on row
			if (y % 2 == 0)
			{   // check to the left
				if (x > 0)
				{
					if (y < GridHeight - 1 && grid[(x - 1) + (y + 1) * GridWidth] == bubbleType) AddCellToList(collected, x - 1, y + 1, bubbleType);
					if (y > 0 && grid[(x - 1) + (y - 1) * GridWidth] == bubbleType) AddCellToList(collected, x - 1, y - 1, bubbleType);
				}
			}
			else
			{   // check to the right
				if (x < GridWidth - 1)
				{
					if (y < GridHeight - 1 && grid[(x + 1) + (y + 1) * GridWidth] == bubbleType) AddCellToList(collected, x + 1, y + 1, bubbleType);
					if (y > 0 && grid[(x + 1) + (y - 1) * GridWidth] == bubbleType) AddCellToList(collected, x + 1, y - 1, bubbleType);
				}
			}
		}

		private void AddCellToList(List<int> collected, int x, int y)
		{
			int idx = y * GridWidth + x;
			if (collected.Contains(idx)) return;
			collected.Add(idx);

			// check if connected left
			if (x > 0 && grid[(x - 1) + y * GridWidth] >= 0) AddCellToList(collected, x - 1, y);

			// check if connected right
			if (x < GridWidth - 1 && grid[(x + 1) + y * GridWidth] >= 0) AddCellToList(collected, x + 1, y);

			// check if connected above
			if (y > 0 && grid[x + (y - 1) * GridWidth] >= 0) AddCellToList(collected, x, y - 1);

			// check if connected below
			if (y < GridHeight - 1 && grid[x + (y + 1) * GridWidth] >= 0) AddCellToList(collected, x, y + 1);

			// for above and below an extra cell to left or right must be checked depending on row
			if (y % 2 == 0)
			{   // check to the left
				if (x > 0)
				{
					if (y < GridHeight - 1 && grid[(x - 1) + (y + 1) * GridWidth] >= 0) AddCellToList(collected, x - 1, y + 1);
					if (y > 0 && grid[(x - 1) + (y - 1) * GridWidth] >= 0) AddCellToList(collected, x - 1, y - 1);
				}
			}
			else
			{   // check to the right
				if (x < GridWidth - 1)
				{
					if (y < GridHeight - 1 && grid[(x + 1) + (y + 1) * GridWidth] >= 0) AddCellToList(collected, x + 1, y + 1);
					if (y > 0 && grid[(x + 1) + (y - 1) * GridWidth] >= 0) AddCellToList(collected, x + 1, y - 1);
				}
			}
		}

		private void IncScore(int c, Vector2 p)
		{
			score += c;
			floatingScores.Add(new ScoreInfo()
			{
				label = new GUIContent("+" + c),
				timer = Random.Range(1f, 2f),
				pos = new Rect(p.x * CellSize + boardRect.x, p.y * CellSize + boardRect.y, 100, 20)
			});
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void DoDraw(Event ev)
		{
			// UI
			GUI.Label(labelRect, string.Format("Score: {0}", score), EditorStyles.largeLabel);

			if (GUI.Button(resetButtonRect, GC_Reset, EditorStyles.miniButtonLeft))
			{
				ResetGame();
			}

			if (GUI.Button(helpButtonRect, GC_Help, EditorStyles.miniButtonRight))
			{
				EdGameHelpWindow.ShowWindow(GC_Label, GC_HelpLines);
			}

			if (ev.type == EventType.Repaint)
			{
				DrawBoard();
				DrawIndicator(ev);
				DrawBubble();

				foreach (ScoreInfo s in floatingScores)
					GUI.Label(s.pos, s.label, scoreStyle);
			}
		}

		private void DrawBoard()
		{
			backStyle.Draw(boardRect, false, false, false, false);

			cellRect.x = boardRect.x + 1 + jitter;
			cellRect.y = boardRect.y + 1;
			float offs = 0f;
			for (int i = 0; i < grid.Length; i++)
			{
				if (grid[i] >= 0)
				{
					GUI.color = BubbleColours[grid[i]];
					GUI.DrawTexture(cellRect, textures[1]);
				}

				cellRect.x += CellSize;
				if ((i+1) % GridWidth == 0)
				{
					offs = offs == 0.0f ? CellSizeHalf : 0.0f;
					cellRect.x = boardRect.x + 1 + offs + jitter;
					cellRect.y += CellSize;
				}
			}

			GUI.color = Color.white;
		}

		private void DrawIndicator(Event ev)
		{
			if (clickAreaRect.Contains(ev.mousePosition)) lastMousePos = Event.current.mousePosition;
			EdGameUtil.DrawTexture(textures[2], pivotPoint, lastMousePos, -360, +360);
		}

		private void DrawBubble()
		{
			cellRect.x = bubblePos.x - CellSizeHalf;
			cellRect.y = bubblePos.y - CellSizeHalf;
			GUI.color = BubbleColours[bubbleType];
			GUI.DrawTexture(cellRect, textures[1]);
			GUI.color = Color.white;
		}

		// ------------------------------------------------------------------------------------------------------------------

		private void GenerateAssets()
		{
			// generate all needed assets so that I do not need any texture resources in the project
			textures = new Texture2D[3];

			Color32 cBlack = new Color32(0, 0, 0, 255);
			Color32 cWhite = new Color32(255, 255, 255, 255);
			Color32 cGrey = new Color32(128, 128, 128, 255);
			Color32 cTrans = new Color32(0, 0, 0, 0);

			// create the background
			textures[0] = new Texture2D(4, 4, TextureFormat.RGBA32, false);
			EdGameUtil.TextureFill(textures[0], cBlack);
			EdGameUtil.TextureRect(textures[0], cWhite, 0, 0, 3, 3);

			// create the cell/bubble texture
			textures[1] = new Texture2D(CellSize, CellSize, TextureFormat.RGBA32, false);
			EdGameUtil.TextureFill(textures[1], cTrans);
			EdGameUtil.TextureDrawCircleFilled(textures[1], cWhite, CellSizeHalf, CellSizeHalf, CellSizeHalf);
			EdGameUtil.TextureDrawCircle(textures[1], cGrey, CellSizeHalf, CellSizeHalf, CellSizeHalf);
			EdGameUtil.TextureDrawCircleFilled(textures[1], cGrey, CellSizeHalf, CellSizeHalf, CellSizeHalf / 3);

			// create indicator texture
			textures[2] = new Texture2D(CellSizeHalf, CellSize * 3, TextureFormat.RGBA32, false);
			EdGameUtil.TextureFill(textures[2], cTrans);
			EdGameUtil.TextureDrawVLine(textures[2], cGrey, textures[2].width / 2, 0, textures[2].height - 1);
			EdGameUtil.TextureDrawLine(textures[2], cGrey, textures[2].width / 2, 0, 0, textures[2].height / 4);
			EdGameUtil.TextureDrawLine(textures[2], cGrey, textures[2].width / 2, 0, textures[2].width - 1, textures[2].height / 4);

			// apply common texture settings
			foreach (Texture2D t in textures)
			{
				t.wrapMode = TextureWrapMode.Clamp;
				t.filterMode = FilterMode.Point;
				t.hideFlags = HideFlags.HideAndDontSave;
			}

			// create the styles
			backStyle = new GUIStyle() { border = new RectOffset(1, 1, 1, 1), normal = { background = textures[0] } };
			scoreStyle = new GUIStyle() { normal = { textColor = Color.green } };
		}

		private void DestroyAssets()
		{
			foreach (Texture2D t in textures)
			{
				if (t != null) DestroyImmediate(t);
			}
		}

		// ------------------------------------------------------------------------------------------------------------------
	}
}