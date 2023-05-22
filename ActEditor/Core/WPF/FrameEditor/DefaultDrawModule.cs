using System;
using System.Collections.Generic;
using ActEditor.Core.DrawingComponents;

namespace ActEditor.Core.WPF.FrameEditor {
	public class DefaultDrawModule : IDrawingModule {
		private readonly Func<List<DrawingComponent>> _getComponents;
		private readonly DrawingPriorityValues _priority;
		private readonly bool _permanent;

		public int DrawingPriority {
			get { return (int)_priority; }
		}

		public List<DrawingComponent> GetComponents() {
			return _getComponents() ?? new List<DrawingComponent>();
		}

		public bool Permanent {
			get { return _permanent; }
		}

		public DefaultDrawModule(Func<List<DrawingComponent>> getComponents, DrawingPriorityValues priority, bool permanent) {
			_getComponents = getComponents;
			_priority = priority;
			_permanent = permanent;
		}
	}
}
