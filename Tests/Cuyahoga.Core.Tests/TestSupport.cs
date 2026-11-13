using System;
using System.Collections.Generic;
using System.IO;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Persistence;
using Xunit;

namespace Cuyahoga.Core.Tests
{
	/// <summary>
	/// A temporary directory that is deleted when the test is done.
	/// </summary>
	public sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuyahoga-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public string File(string name) => System.IO.Path.Combine(Path, name);

		public void Dispose()
		{
			if (Directory.Exists(Path))
			{
				Directory.Delete(Path, true);
			}
		}
	}

	/// <summary>
	/// Builds small site structures for domain tests. Permission checks compare roles by reference, so tests reuse the
	/// role instances.
	/// </summary>
	public static class Build
	{
		public static Role Role(int id, string name, AccessLevel accessLevels) =>
			new Role { Id = id, Name = name, PermissionLevel = (int)accessLevels };

		public static Role Anonymous() => Role(4, "Anonymous user", AccessLevel.Anonymous);
		public static Role Editor() => Role(2, "Editor", AccessLevel.Authenticated | AccessLevel.Editor);

		public static User User(params Role[] roles)
		{
			var user = new User { UserName = "someone" };
			foreach (var role in roles)
			{
				user.Roles.Add(role);
			}
			return user;
		}

		/// <summary>
		/// A node with an empty list of child nodes (like a node loaded by NHibernate). With a parent, the node is added
		/// as its last child.
		/// </summary>
		public static Node Node(int id, string title, Node parent = null)
		{
			var node = new Node { Id = id, Title = title, ChildNodes = new List<Node>() };
			if (parent != null)
			{
				node.ParentNode = parent;
				node.Position = parent.ChildNodes.Count;
				parent.ChildNodes.Add(node);
			}
			return node;
		}

		public static Node Allow(this Node node, Role role, bool view = true, bool edit = false)
		{
			node.NodePermissions.Add(new NodePermission { Node = node, Role = role, ViewAllowed = view, EditAllowed = edit });
			return node;
		}

		public static Section Section(int id, Node node, string placeholder)
		{
			var section = new Section { Id = id, Title = "Section " + id, Node = node, PlaceholderId = placeholder };
			section.CalculateNewPosition();
			node.Sections.Add(section);
			return section;
		}

		public static Section Allow(this Section section, Role role, bool view = true, bool edit = false)
		{
			section.SectionPermissions.Add(new SectionPermission { Section = section, Role = role, ViewAllowed = view, EditAllowed = edit });
			return section;
		}
	}

	/// <summary>
	/// Context storage for tests that reach the unit of work (TransactionContext, SessionManager). Core has no default
	/// storage; the web site sets HttpContextStorage at application start.
	/// </summary>
	public sealed class DictionaryContextStorage : IContextStorage
	{
		private readonly Dictionary<string, object> _items = new Dictionary<string, object>();

		public object Get(string key) => _items.TryGetValue(key, out object value) ? value : null;

		public void Set(string key, object value)
		{
			if (value == null)
			{
				_items.Remove(key);
			}
			else
			{
				_items[key] = value;
			}
		}
	}

	/// <summary>
	/// Tests that change process-wide state (environment variables) don't run in parallel with other tests.
	/// </summary>
	[CollectionDefinition(Name, DisableParallelization = true)]
	public class ProcessStateCollection
	{
		public const string Name = "Process state";
	}
}
