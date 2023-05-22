using System.Collections.Generic;

namespace ActEditor.Core.DrawingComponents {
	public interface IDrawingModule {
		int DrawingPriority { get; }
		List<DrawingComponent> GetComponents();
		bool Permanent { get; }
	}
}
