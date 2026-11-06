namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System;
	using System.IO;
	using System.Text;

	using Newtonsoft.Json;

	/// <summary>Durable, agent-local recovery journal, accessed under the category update mutex.</summary>
	public class CategoryIconPropagationStore
	{
		private readonly string directory;

		/// <summary>Initializes a journal in the shared application data directory.</summary>
		public CategoryIconPropagationStore()
			: this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Skyline", "ServiceManagement", "CategoryIconPropagation"))
		{
		}

		/// <summary>Initializes a journal in the specified directory.</summary>
		/// <param name="directory">Recovery journal directory.</param>
		public CategoryIconPropagationStore(string directory)
		{
			this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
		}

		/// <summary>Reads an unfinished propagation, if present.</summary>
		/// <param name="categoryId">Category identifier.</param>
		/// <returns>The pending propagation, or null.</returns>
		public CategoryIconPropagation Read(Guid categoryId)
		{
			var path = Path.Combine(directory, $"{categoryId:N}.json");
			if (!File.Exists(path))
			{
				return null;
			}

			var pending = JsonConvert.DeserializeObject<CategoryIconPropagation>(File.ReadAllText(path));
			if (pending?.PreviousIcon == null || pending.Icon == null)
			{
				throw new InvalidDataException("Invalid category icon propagation journal.");
			}

			return pending;
		}

		/// <summary>Persists intent before any service or category write.</summary>
		/// <param name="categoryId">Category identifier.</param>
		/// <param name="pending">Propagation to recover after interruption.</param>
		public void Save(Guid categoryId, CategoryIconPropagation pending)
		{
			Directory.CreateDirectory(directory);
			var path = Path.Combine(directory, $"{categoryId:N}.json");
			var temporaryPath = path + ".tmp";
			var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(pending));
			using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				stream.Write(data, 0, data.Length);
				stream.Flush(true);
			}

			// Publish only a complete journal. An existing pending operation must be recovered first.
			File.Move(temporaryPath, path);
		}

		/// <summary>Removes the journal only after the category and inherited services have been saved.</summary>
		/// <param name="categoryId">Category identifier.</param>
		public void Complete(Guid categoryId)
		{
			File.Delete(Path.Combine(directory, $"{categoryId:N}.json"));
		}
	}

	/// <summary>Icon transition that must finish before another category update is accepted.</summary>
	public sealed class CategoryIconPropagation
	{
		/// <summary>Gets or sets the previous inherited icon.</summary>
		public string PreviousIcon { get; set; }

		/// <summary>Gets or sets the target inherited icon.</summary>
		public string Icon { get; set; }
	}
}
