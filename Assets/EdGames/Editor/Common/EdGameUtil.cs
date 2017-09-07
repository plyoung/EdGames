using System.Collections.Generic;
using UnityEngine;


namespace EdGames
{
	public static class EdGameUtil
	{
		// ------------------------------------------------------------------------------------------------------------------
		#region draw to texture

		/// <summary> fills a texture with colour </summary>
		public static void TextureFill(Texture2D t, Color32 c)
		{
			Color32[] bitmap = t.GetPixels32();
			for (int i = 0; i < bitmap.Length; i++)
			{
				bitmap[i] = c;
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		/// <summary> draw a rect on texture with colour. note 0x0 is at bottom-left of texture </summary>
		public static void TextureRect(Texture2D t, Color32 c, int x, int y, int w, int h)
		{
			x = Mathf.Clamp(x, 0, t.width - 1);
			y = Mathf.Clamp(y, 0, t.height - 1);
			if (h + y > t.height - 1) h = t.height -1 - y;
			if (w + x > t.width - 1) w = t.width -1 - x;

			Color32[] bitmap = t.GetPixels32();
			for (int i = x; i <= x + w; i++)
			{
				bitmap[y * t.width + i] = c;
				bitmap[(y + h) * t.width + i] = c;
			}

			for (int i = y; i <= y + h; i++)
			{
				bitmap[i * t.width + x] = c;
				bitmap[i * t.width + x + w] = c;
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		/// <summary> fills a rect area of texture with colour. note 0x0 is at bottom-left of texture </summary>
		public static void TextureFillRect(Texture2D t, Color32 c, int x, int y, int w, int h)
		{
			x = Mathf.Clamp(x, 0, t.width - 1);
			y = Mathf.Clamp(y, 0, t.height - 1);
			if (h + y > t.height) h = t.height - y;
			if (w + x > t.width) w = t.width - x;

			Color32[] bitmap = t.GetPixels32();
			for (int i = x; i < x + w; i++)
			{
				for (int j = y; j < y + h; j++)
				{
					bitmap[j * t.width + i] = c;
				}
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		/// <summary> draw vertical line on texture (towards top). note 0x0 is at bottom-left of texture </summary>
		public static void TextureDrawVLine(Texture2D t, Color32 c, int x, int y, int h)
		{
			x = Mathf.Clamp(x, 0, t.width - 1);
			y = Mathf.Clamp(y, 0, t.height - 1);
			if (y + h > t.height) h = t.height - y;

			Color32[] bitmap = t.GetPixels32();
			for (int j = y; j < y + h; j++)
			{
				bitmap[j * t.width + x] = c;
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		/// <summary> draw horizontal line on texture (towards right). note 0x0 is at bottom-left of texture </summary>
		public static void TextureDrawHLine(Texture2D t, Color32 c, int x, int y, int w)
		{
			x = Mathf.Clamp(x, 0, t.width - 1);
			y = Mathf.Clamp(y, 0, t.height - 1);
			if (x + w > t.width) w = t.width - x;

			Color32[] bitmap = t.GetPixels32();
			int offs = y * t.width;
			for (int i = x; i < x + w; i++)
			{
				bitmap[offs + i] = c;
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		/// <summary> draw line on texture. note 0x0 is at bottom-left of texture </summary>
		public static void TextureDrawLine(Texture2D t, Color32 c, int x1, int y1, int x2, int y2)
		{
			x1 = Mathf.Clamp(x1, 0, t.width - 1);
			y1 = Mathf.Clamp(y1, 0, t.height - 1);
			x2 = Mathf.Clamp(x2, 0, t.width - 1);
			y2 = Mathf.Clamp(y2, 0, t.height - 1);

			Color32[] bitmap = t.GetPixels32();

			int dx = Mathf.Abs(x2 - x1);
			int sx = x1 < x2 ? 1 : -1;
			int dy = Mathf.Abs(y2 - y1);
			int sy = y1 < y2 ? 1 : -1;
			int e1 = (dx > dy ? dx : -dy) / 2;
			int e2;
			for (;;)
			{
				bitmap[y1 * t.width + x1] = c;
				if (x1 == x2 && y1 == y2) break;
				e2 = e1;
				if (e2 > -dx) { e1 -= dy; x1 += sx; }
				if (e2 < dy) { e1 += dx; y1 += sy; }
			}

			t.SetPixels32(bitmap);
			t.Apply();
		}

		public static void TextureDrawCircle(Texture2D t, Color32 c, int cx, int cy, int r)
		{
			int d = (5 - r * 4) / 4;
			int x = 0;
			int y = r;

			Color32[] bitmap = t.GetPixels32();

			do
			{
				if (cx + x >= 0 && cx + x <= t.width - 1 && cy + y >= 0 && cy + y <= t.height - 1) bitmap[(cy + y) * t.width + (cx + x)] = c;
				if (cx + x >= 0 && cx + x <= t.width - 1 && cy - y >= 0 && cy - y <= t.height - 1) bitmap[(cy - y) * t.width + (cx + x)] = c;
				if (cx - x >= 0 && cx - x <= t.width - 1 && cy + y >= 0 && cy + y <= t.height - 1) bitmap[(cy + y) * t.width + (cx - x)] = c;
				if (cx - x >= 0 && cx - x <= t.width - 1 && cy - y >= 0 && cy - y <= t.height - 1) bitmap[(cy - y) * t.width + (cx - x)] = c;
				if (cx + y >= 0 && cx + y <= t.width - 1 && cy + x >= 0 && cy + x <= t.height - 1) bitmap[(cy + x) * t.width + (cx + y)] = c;
				if (cx + y >= 0 && cx + y <= t.width - 1 && cy - x >= 0 && cy - x <= t.height - 1) bitmap[(cy - x) * t.width + (cx + y)] = c;
				if (cx - y >= 0 && cx - y <= t.width - 1 && cy + x >= 0 && cy + x <= t.height - 1) bitmap[(cy + x) * t.width + (cx - y)] = c;
				if (cx - y >= 0 && cx - y <= t.width - 1 && cy - x >= 0 && cy - x <= t.height - 1) bitmap[(cy - x) * t.width + (cx - y)] = c;
				if (d < 0)
				{
					d += 2 * x + 1;
				}
				else
				{
					d += 2 * (x - y) + 1;
					y--;
				}
				x++;
			} while (x <= y);

			t.SetPixels32(bitmap);
			t.Apply();
		}

		public static void TextureDrawCircleFilled(Texture2D t, Color32 c, int cx, int cy, int r)
		{
			TextureDrawCircle(t, c, cx, cy, r);
			TextureFloodFill(t, c, cx, cy);
		}

		public static void TextureFloodFill(Texture2D t, Color c, int x, int y)
		{
			Color oldCol = t.GetPixel(x, y);
			Vector2 pt = new Vector2(x, y);
			Queue<Vector2> q = new Queue<Vector2>();
			q.Enqueue(pt);
			while (q.Count > 0)
			{
				Vector2 n = q.Dequeue();
				if (t.GetPixel((int)n.x, (int)n.y) != oldCol) continue;

				Vector2 w = n, e = new Vector2(n.x + 1, n.y);
				while ((w.x >= 0) && (t.GetPixel((int)w.x, (int)w.y) == oldCol))
				{
					t.SetPixel((int)w.x, (int)w.y, c);
					if ((w.y > 0) && (t.GetPixel((int)w.x, (int)w.y - 1) == oldCol)) q.Enqueue(new Vector2(w.x, w.y - 1));
					if ((w.y < t.height - 1) && (t.GetPixel((int)w.x, (int)w.y + 1) == oldCol)) q.Enqueue(new Vector2(w.x, w.y + 1));
					w.x--;
				}
				while ((e.x <= t.width - 1) && (t.GetPixel((int)e.x, (int)e.y) == oldCol))
				{
					t.SetPixel((int)e.x, (int)e.y, c);
					if ((e.y > 0) && (t.GetPixel((int)e.x, (int)e.y - 1) == oldCol)) q.Enqueue(new Vector2(e.x, e.y - 1));
					if ((e.y < t.height - 1) && (t.GetPixel((int)e.x, (int)e.y + 1) == oldCol)) q.Enqueue(new Vector2(e.x, e.y + 1));
					e.x++;
				}
			}

			t.Apply();
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------------
		#region GUI

		/// <summary> draw texture rotated around point pointing towards p2. </summary>
		public static void DrawTexture(Texture2D t, Vector2 p1, Vector2 p2, float minR, float maxR)
		{
			p1 += new Vector2(0, 22); // hack!
			p2 += new Vector2(0, 22); // hack!
			float r = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * 180f / Mathf.PI - 90f;
			r = Mathf.Clamp(r, minR, maxR);

			GUI.EndClip(); // hack!
			Matrix4x4 m = GUI.matrix;
			GUIUtility.RotateAroundPivot(r, p1);
			GUI.DrawTexture(new Rect(p1.x - t.width / 2, p1.y, t.width, t.height), t);
			GUI.matrix = m;
			GUI.BeginClip(new Rect(0, 22, Screen.width, Screen.height)); // hack!
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------------
		#region misc

		public static int[,] RotateMatrix(int[,] m, int n)
		{
			int[,] res = new int[n, n];
			for (int i = 0; i < n; i++)
			{
				for (int j = 0; j < n; j++)
				{
					res[i, j] = m[n - j - 1, i];
				}
			}
			return res;
		}

		public static int[,] RotateMatrixReverse(int[,] m, int n)
		{
			int[,] res = new int[n, n];
			for (int i = 0; i < n; i++)
			{
				for (int j = 0; j < n; j++)
				{
					res[i, j] = m[j, n - i - 1];
				}
			}
			return res;
		}

		public static void CalcCellPos(Rect r, Vector2 p, int cellSize, bool floor, out int x, out int y)
		{
			p = new Vector2(p.x - r.x, p.y - r.y);
			if (floor)
			{
				x = Mathf.FloorToInt(p.x / cellSize);
				y = Mathf.FloorToInt(p.y / cellSize);
			}
			else
			{
				x = Mathf.RoundToInt(p.x / cellSize);
				y = Mathf.RoundToInt(p.y / cellSize);
			}
		}

		public static void IndexToXY(int width, int idx, out int x, out int y)
		{
			x = idx % width;
			y = (idx - x) / width;
		}

		#endregion
		// ------------------------------------------------------------------------------------------------------------------
	}
}