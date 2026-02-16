using System.Collections.Generic;
using System.Windows;

namespace ActEditor.Core {
	public class UIElementProvider<T> where T : UIElement, new() {
		public Dictionary<T, int> ImageIndexes = new Dictionary<T, int>();
		public Queue<int> UnusedImages = new Queue<int>();
		public List<T> Images = new List<T>();

		public T GetElement() {
			if (UnusedImages.Count == 0) {
				var img = new T();
				Images.Add(img);
				ImageIndexes[img] = Images.Count - 1;
				img.Visibility = Visibility.Visible;
				return img;
			}
			else {
				int idx = UnusedImages.Dequeue();
				var img = Images[idx];
				img.Visibility = Visibility.Visible;
				return img;
			}
		}

		public void RemoveElement(T image) {
			UnusedImages.Enqueue(ImageIndexes[image]);
			image.Visibility = Visibility.Collapsed;
		}

		public void Clear() {
			UnusedImages.Clear();
			Images.Clear();
			ImageIndexes.Clear();
		}
	}
}
