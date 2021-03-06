using System;
using NUnit.Framework;
using OpenIDE.Core.Definitions;

namespace OpenIDE.Core.Tests.Definitions
{
	[TestFixture]
	public class DefinitionCacheTests
	{
		[Test]
		public void When_given_an_unexisting_command_it_returns_null() {
			Assert.That(new DefinitionCache().Get(new[] {"cmd1"}), Is.Null);
		}

		[Test]
		public void Can_find_added_command() {
			var type = DefinitionCacheItemType.Script;
			var time = DateTime.Now;
			var cache = new DefinitionCache();
			cache
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "cmd2", "")
						.Append(type, "", time, true, "cmd3", "");
			Assert.That(cache.Get(new[] {"cmd1","cmd2"}).Name, Is.EqualTo("cmd2"));
		}

		[Test]
		public void Stops_at_first_user_defined_content_command() {
			var type = DefinitionCacheItemType.Script;
			var time = DateTime.Now;
			var cache = new DefinitionCache();
			cache
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "USER_DEFINED_ARE_UPPER_CASE", "")
						.Append(type, "", time, true, "cmd3", "");
			Assert.That(cache.Get(new[] {"cmd1","bleh","cmd3"}).Name, Is.EqualTo("cmd1"));
		}

		[Test]
		public void Stops_at_first_optional_argument() {
			var type = DefinitionCacheItemType.Script;
			var time = DateTime.Now;
			var cache = new DefinitionCache();
			cache
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "cmd2", "")
						.Append(type, "", time, true, "cmd3", "")
							.Append(type, "", time, false, "-g", "");
			Assert.That(cache.Get(new[] {"cmd1", "-g","cmd2","cmd3"}).Name, Is.EqualTo("cmd1"));
		}

		[Test]
		public void Can_merge_two_definition_caches() {
			var type = DefinitionCacheItemType.Script;
			var time = DateTime.Now;
			var cache = new DefinitionCache();
			cache
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "cmd2", "")
						.Append(type, "", time, true, "cmd3", "")
							.Append(type, "", time, false, "-g", "");
			var another = new DefinitionCache();
			another	
				.Add(type, "", time, true, "cmdAnother", "")
					.Append(type, "", time, true, "cmdAnother2", "");
			cache.Merge(another);
			Assert.That(cache.Definitions.Length, Is.EqualTo(2));
		}

		[Test]
		public void When_merging_two_definition_caches_it_will_not_add_duplicate_commands() {
			var type = DefinitionCacheItemType.Script;
			var time = DateTime.Now;
			var cache = new DefinitionCache();
			cache
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "cmd2", "")
						.Append(type, "", time, true, "cmd3", "")
							.Append(type, "", time, false, "-g", "");
			var another = new DefinitionCache();
			another	
				.Add(type, "", time, true, "cmd1", "")
					.Append(type, "", time, true, "cmdAnother", "");
			cache.Merge(another);
			Assert.That(cache.Definitions.Length, Is.EqualTo(1));
		}

		[Test]
		public void Can_fetch_the_oldest_element_in_the_cache() {
			var type = DefinitionCacheItemType.Script;
			var time = new DateTime(2012,1,1,0,0,0);
			var cache = new DefinitionCache();
			cache
				.Add(type, "", DateTime.Now, true, "cmd1", "")
					.Append(type, "", DateTime.Now, true, "cmd2", "")
						.Append(type, "", DateTime.Now, true, "cmd3", "")
							.Append(type, "", time, false, "-g", "");
			cache	
				.Add(type, "", DateTime.Now, true, "cmd1-1", "")
					.Append(type, "", DateTime.Now, true, "cmdAnother", "");
			Assert.That(cache.GetOldestItem().Name, Is.EqualTo("-g"));
		}

		[Test]
		public void Can_fetch_the_oldest_element_of_a_spesific_location_in_the_cache() {
			var type = DefinitionCacheItemType.Script;
			var cache = new DefinitionCache();
			cache
				.Add(type, "loc1", DateTime.Now, true, "cmd1", "")
					.Append(type, "loc1", DateTime.Now, true, "cmd2", "")
						.Append(type, "loc1", new DateTime(2012,10,1,0,0,0), true, "cmd3", "")
							.Append(type, "loc2", new DateTime(2012,1,1,0,0,0), false, "-g", "");
			Assert.That(cache.GetOldestItem("loc1").Name, Is.EqualTo("cmd3"));
		}

		[Test]
		public void Can_fetch_locations_in_the_cache() {
			var type = DefinitionCacheItemType.Script;
			var cache = new DefinitionCache();
			cache
				.Add(type, "loc1", DateTime.Now, true, "cmd1", "")
					.Append(type, "loc1", DateTime.Now, true, "cmd2", "")
						.Append(DefinitionCacheItemType.Language, "loc3", new DateTime(2012,10,1,0,0,0), true, "cmd3", "")
							.Append(type, "loc2", new DateTime(2012,1,1,0,0,0), false, "-g", "");
			Assert.That(cache.GetLocations(DefinitionCacheItemType.Script).Length, Is.EqualTo(2));
		}

		[Test]
		public void Can_override_existing_command() {
			var cache = new DefinitionCache();
			cache
				.Add(DefinitionCacheItemType.BuiltIn, "l1", DateTime.Now, true, "cmd1", "")
					.Append(DefinitionCacheItemType.BuiltIn, "l1", DateTime.Now, true, "cmd1-1", "");
			cache
				.Add(DefinitionCacheItemType.Script, "l2", DateTime.Now, true, "cmd1", "")
					.Append(DefinitionCacheItemType.Script, "l2", DateTime.Now, true, "override-1", "");
			var item = cache.Get(new[] { "cmd1" });
			Assert.That(item.Type, Is.EqualTo(DefinitionCacheItemType.Script));
			Assert.That(item.Location, Is.EqualTo("l2"));
			Assert.That(item.Parameters[0].Name, Is.EqualTo("override-1"));
		}

		[Test]
		public void Can_get_built_in_command_hidden_by_override() {
			var cache = new DefinitionCache();
			cache
				.Add(DefinitionCacheItemType.BuiltIn, "l1", DateTime.Now, true, "cmd1", "")
					.Append(DefinitionCacheItemType.BuiltIn, "l1", DateTime.Now, true, "cmd1-1", "");
			cache
				.Add(DefinitionCacheItemType.Script, "l2", DateTime.Now, true, "cmd1", "")
					.Append(DefinitionCacheItemType.Script, "l2", DateTime.Now, true, "override-1", "");
			var item = cache.GetBuiltIn(new[] { "cmd1" });
			Assert.That(item.Type, Is.EqualTo(DefinitionCacheItemType.BuiltIn));
			Assert.That(item.Location, Is.EqualTo("l1"));
			Assert.That(item.Parameters[0].Name, Is.EqualTo("cmd1-1"));
		}
	}
}