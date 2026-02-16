using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilities;

namespace ActEditor.Core.Test {
	public class ColorIndexNode {
		public Dictionary<byte, int> Counts = new Dictionary<byte, int>();
		public _GrfColorLab Lab;
	}

	public class ColorAdjacencyGraph {
		public const int PaletteColorCount = 256;
		public ColorIndexNode[] Nodes = new ColorIndexNode[PaletteColorCount];
		public byte[] Palette;
		public int[] Usage = new int[PaletteColorCount];

		public ColorAdjacencyGraph(byte[] palette) {
			Palette = palette;

			for (int i = 0; i < PaletteColorCount; i++) {
				Nodes[i] = new ColorIndexNode();
				Nodes[i].Lab = _GrfColorLab.From(Palette[4 * i + 0], Palette[4 * i + 1], Palette[4 * i + 2]);
			}
		}

		public void AddSprite(GrfImage image) {
			unsafe {
				fixed (byte* pPixelsBase = image.Pixels) {
					byte* pPixelsEnd = pPixelsBase + image.Pixels.Length;
					byte* pPixels = pPixelsBase;
					int size = image.Palette.Length;
					int[] locs = { -image.Width - 1, -image.Width, -image.Width + 1, 1, image.Width + 1, image.Width, image.Width - 1, -1 };
					int x = 0;
					int y = 0;

					while (pPixels < pPixelsEnd) {
						if (*pPixels != 0) {
							ColorIndexNode node = Nodes[*pPixels];

							Usage[*pPixels]++;

							for (int i = 0; i < locs.Length; i++) {
								byte* pPixel = pPixels + locs[i];

								if (pPixel < pPixelsEnd && pPixel >= pPixelsBase) {
									if (*pPixel == 0 || *pPixel == *pPixels)
										continue;

									if (!node.Counts.ContainsKey(*pPixel))
										node.Counts[*pPixel] = 1;
									else
										node.Counts[*pPixel]++;
								}
							}
						}

						pPixels++;
						x++;

						if (x >= image.Width) {
							x = 0;
							y++;
						}
					}
				}
			}
		}

		public Dictionary<int, int> Redirection = new Dictionary<int, int>();

		public byte[] FormPalette() {
			Redirection.Clear();
			List<ColorIndexNode> newNodes = new List<ColorIndexNode>();
			bool[] used = new bool[256];
			used[0] = true;

			List<List<int>> chains = new List<List<int>>();

			Dictionary<int, int> weightUsage = new Dictionary<int, int>();

			for (int i = 0; i < PaletteColorCount; i++) {
				weightUsage[i] = Usage[i];
			}

			// three-pass
			for (int p = 0; p < 3; p++) {
				foreach (var entry in weightUsage.OrderByDescending(k => k.Value)) {
					int i = entry.Key;

					if (used[i])
						continue;

					var node = Nodes[i];

					if (node.Counts.Count == 0)
						continue;

					HashSet<int> chain = new HashSet<int>();
				
					if (_trySolveChain(i, node, used, chain, p == 0 ? 10 : 20, p >= 2)) {
						foreach (var idx in chain) {
							used[idx] = true;
						}

						chains.Add(chain.ToList());
					}
				}
			}

			byte[] pal = new byte[256 * 4];
			pal[0] = Palette[0];
			pal[1] = Palette[1];
			pal[2] = Palette[2];
			pal[3] = Palette[3];

			Redirection[0] = 0;

			{
				int i = 8;

				foreach (var chain in chains) {
					var cols = chain.OrderByDescending(p => Palette[4 * p + 0] + Palette[4 * p + 1] + Palette[4 * p + 2]).ToList();

					for (int j = 0; j < 8; j++) {
						pal[4 * i + 4 * j + 0] = Palette[4 * cols[j] + 0];
						pal[4 * i + 4 * j + 1] = Palette[4 * cols[j] + 1];
						pal[4 * i + 4 * j + 2] = Palette[4 * cols[j] + 2];
						pal[4 * i + 4 * j + 3] = Palette[4 * cols[j] + 3];

						Redirection[cols[j]] = i + j;
					}

					i += 8;
				}

				List<int> unused = new List<int>();

				for (; i < 256; i++)
					unused.Add(i);

				for (i = 1; i < 8; i++)
					unused.Add(i);

				for (i = 0; i < 256; i++) {
					if (used[i])
						continue;

					int j = unused[0];
					unused.RemoveAt(0);

					pal[4 * j + 0] = Palette[4 * i + 0];
					pal[4 * j + 1] = Palette[4 * i + 1];
					pal[4 * j + 2] = Palette[4 * i + 2];
					pal[4 * j + 3] = Palette[4 * i + 3];

					Redirection[i] = j;
				}
			}

			Z.F();

			return pal;
		}

		public void ApplyRedirect(GrfImage image) {
			for (int i = 0; i < image.Pixels.Length; i++) {
				image.Pixels[i] = (byte)Redirection[image.Pixels[i]];
			}
		}

		private bool _trySolveChain(int idx, ColorIndexNode node, bool[] used, HashSet<int> chain, float maxDist, bool all) {
			if (chain.Count == 0) {
				chain.Add(idx);
			}

			int added;

			while (true) {
				added = -1;

				foreach (var entry in node.Counts.OrderByDescending(p => p.Value)) {
					if (used[entry.Key] || chain.Contains(entry.Key))
						continue;

					var dist = Distance(node.Lab, Nodes[entry.Key].Lab);

					if (dist > maxDist)
						continue;

					chain.Add(entry.Key);
					added = entry.Key;
					break;
				}

				if (chain.Count >= 8)
					return true;

				if (added < 0) {
					if (all) {
						for (int i = 1; i < 256; i++) {
							if (used[i] || chain.Contains(i))
								continue;

							var dist = Distance(node.Lab, Nodes[i].Lab);

							if (dist > maxDist)
								continue;

							chain.Add(i);
							added = i;
							break;
						}
					}

					if (added < 0) {
						break;
					}
				}

				if (chain.Count < 8) {
					node = Nodes[added];
				}
			};

			return false;
		}

		public double Distance(_GrfColorLab a, _GrfColorLab b) {
			double dL = a.L - b.L;
			double da = a.A - b.A;
			double db = a.B - b.B;
			return Math.Sqrt(dL * dL + da * da + db * db);
		}
	}
}
