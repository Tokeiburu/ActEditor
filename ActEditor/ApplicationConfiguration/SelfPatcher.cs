using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActEditor.Core;
using GRF.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActEditor.ApplicationConfiguration {
	/// <summary>
	/// Class imported from GrfEditor
	/// </summary>
	public class SelfPatcher {
		public static List<int> PatchIds = new List<int>();

		static SelfPatcher() {
			PatchIds.Add(13);
			PatchIds.Add(16);
			PatchIds.Add(24);
			PatchIds.Add(125);
			PatchIds.Add(126);
			PatchIds.Add(127);
			PatchIds.Add(128);
			PatchIds.Add(131);
			PatchIds.Add(132);
			PatchIds.Add(133);
			PatchIds.Add(134);
			PatchIds.Add(137);
		}

		public static void SelfPatch() {
			int currentPatchId = ActEditorConfiguration.PatchId;

			foreach (var patchId in PatchIds) {
				if (patchId >= currentPatchId) {
					ApplyPatchId(patchId);
					currentPatchId = patchId + 1;
				}
			}

			ActEditorConfiguration.PatchId = currentPatchId;
		}

		public static void ApplyPatchId(int patchId) {
			try {
				string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath);

				switch (patchId) {
					case 13:
					case 16:
						foreach (string resource in ScriptLoader.ScriptNames) {
							foreach (string file in new string[] { resource + ".cs", resource + ".dll" }) {
								GrfPath.Delete(Path.Combine(path, file));
							}
						}
						break;
					case 24:
					case 125:
						foreach (string resource in ScriptLoader.ScriptNames) {
							foreach (string file in new string[] { resource + ".cs", resource + ".dll" }) {
								GrfPath.Delete(Path.Combine(path, file));
							}
						}

						GrfPath.Delete(Path.Combine(path, "script3_mirror_frame.cs"));
						GrfPath.Delete(Path.Combine(path, "script3_mirror_frame.dll"));
						GrfPath.Delete(Path.Combine(path, "script0_magnifyAll.cs"));
						GrfPath.Delete(Path.Combine(path, "script0_magnifyAll.dll"));
						break;
					case 126:
						GrfPath.Delete(Path.Combine(path, "script2_expand.cs"));
						GrfPath.Delete(Path.Combine(path, "script2_expand.dll"));

						GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
						GrfPath.Delete(Path.Combine(path, "script7_add_effect1.dll"));

						GrfPath.Delete(Path.Combine(path, "script8_add_frames.cs"));
						GrfPath.Delete(Path.Combine(path, "script8_add_frames.dll"));

						GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.cs"));
						GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.dll"));
						break;
					case 127:
						GrfPath.Delete(Path.Combine(path, "script0_magnify.cs"));
						GrfPath.Delete(Path.Combine(path, "script0_magnify.dll"));
						break;
					case 128:
						GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
						GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));
						break;
					case 131:
						GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.cs"));
						GrfPath.Delete(Path.Combine(path, "script9_chibi_grf.dll"));

						GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
						GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));

						GrfPath.Delete(Path.Combine(path, "script11_palette_sheet.cs"));
						GrfPath.Delete(Path.Combine(path, "script11_palette_sheet.dll"));
						break;
					case 132:
						GrfPath.Delete(Path.Combine(path, "script6_merge_layers.cs"));
						GrfPath.Delete(Path.Combine(path, "script6_merge_layers.dll"));
						break;
					case 133:
						GrfPath.Delete(Path.Combine(path, "script0_magnify.cs"));
						GrfPath.Delete(Path.Combine(path, "script0_magnify.dll"));

						GrfPath.Delete(Path.Combine(path, "script2_expand.cs"));
						GrfPath.Delete(Path.Combine(path, "script2_expand.dll"));

						GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
						GrfPath.Delete(Path.Combine(path, "script7_add_effect1.dll"));

						GrfPath.Delete(Path.Combine(path, "script8_add_frame.cs"));
						GrfPath.Delete(Path.Combine(path, "script8_add_frame.dll"));

						GrfPath.Delete(Path.Combine(path, "script10_trim_images.cs"));
						GrfPath.Delete(Path.Combine(path, "script10_trim_images.dll"));

						GrfPath.Delete(Path.Combine(path, "sprites.conf"));
						GrfPath.Delete(Path.Combine(path, "sprites_old.conf"));
						break;
					case 134:
						GrfPath.Delete(Path.Combine(path, "script7_add_effect1.cs"));
						GrfPath.Delete(Path.Combine(path, "script8_add_frames.cs"));
						break;
					case 137:
						GrfPath.Delete(Path.Combine(path, "sprites.conf"));
						GrfPath.Delete(Path.Combine(path, "sprites_old.conf"));
						break;
				}
			}
			catch {

			}
		}
	}
}