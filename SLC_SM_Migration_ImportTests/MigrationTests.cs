namespace SLC_SM_Migration_ImportTests
{
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
	using Skyline.DataMiner.Net.GenericEnums;
	using Skyline.DataMiner.Net.Sections;

	using SLCSMMigrationExport.Migration;

	using SLCSMMigrationImport.Migration;

	using Cpv = SLCSMMigrationImport.Migration.KnownIds.ConfigurationParameterValue;
	using ImportField = SLCSMMigrationImport.Migration.ExportedField;
	using ImportInstance = SLCSMMigrationImport.Migration.ExportedInstance;

	[TestClass]
	public class ValueCodecTests
	{
		[TestMethod]
		public void ExportedValues_RoundTripToTheNewFieldTypes()
		{
			var date = new DateTime(2025, 3, 4, 5, 6, 7, 891, DateTimeKind.Utc);
			var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

			AssertRoundTrip(date, typeof(DateTime));
			AssertRoundTrip(0.1 + 0.2, typeof(double));
			AssertRoundTrip(ids, typeof(List<Guid>));
		}

		[TestMethod]
		public void GenericEnumValue_IsAcceptedOnlyWhenItIsAnOptionOfTheNewEnum()
		{
			var options = new GenericEnum<int>();
			options.AddEntry("Number", 0);
			var field = new GenericEnumFieldDescriptor { ID = new FieldDescriptorID(Guid.NewGuid()), FieldType = typeof(GenericEnum<int>), GenericEnumInstance = options };

			Assert.IsTrue(ValueCodec.TryDecode(Value("Int32", "0"), field, out var wrapper, out _));
			Assert.AreEqual(0, wrapper.Value);
			Assert.IsFalse(ValueCodec.TryDecode(Value("Int32", "7"), field, out _, out string error));
			StringAssert.Contains(error, "not a valid option");
		}

		[TestMethod]
		public void IncompatibleValue_IsRejectedWithAClearError()
		{
			var field = TestModel.Field(Guid.NewGuid(), "Count", typeof(long));

			Assert.IsFalse(ValueCodec.TryDecode(Value("String", "abc"), field, out _, out string error));
			StringAssert.Contains(error, "cannot be converted to Int64");
			Assert.IsFalse(ValueCodec.TryDecode(new ImportField { ValueType = "Int64", IsList = true, Values = { "1", "2" } }, field, out _, out error));
			StringAssert.Contains(error, "single value");
		}

		private static ImportField Value(string type, string value) => new ImportField { FieldId = Guid.NewGuid(), ValueType = type, Values = { value } };

		private static void AssertRoundTrip(object value, Type fieldType)
		{
			var exported = DomExporter.Encode(Guid.NewGuid(), ValueWrapperFactory.Create(value));
			var json = Newtonsoft.Json.JsonConvert.SerializeObject(exported);
			var imported = Newtonsoft.Json.JsonConvert.DeserializeObject<ImportField>(json);

			Assert.IsTrue(ValueCodec.TryDecode(imported, TestModel.Field(Guid.NewGuid(), "f", fieldType), out var wrapper, out string error), error);
			Assert.AreEqual(fieldType, wrapper.Type);
			Assert.AreEqual(ValueCodec.Canonical(ValueWrapperFactory.Create(value)), ValueCodec.Canonical(wrapper));
		}
	}

	[TestClass]
	public class StatusPathTests
	{
		private static readonly BehaviorInfo Behavior = new BehaviorInfo("a", new List<DomStatus>(), new List<DomStatusTransition>
		{
			new DomStatusTransition("a_b", "a", "b"),
			new DomStatusTransition("b_c", "b", "c"),
			new DomStatusTransition("a_x", "a", "x"),
		});

		[TestMethod]
		public void FindPath_ReturnsShortestChain_EmptyForSameStatus_NullWhenUnreachable()
		{
			CollectionAssert.AreEqual(new[] { "a_b", "b_c" }, Behavior.FindPath("a", "c").Select(t => t.Id).ToArray());
			Assert.AreEqual(0, Behavior.FindPath("b", "b").Count);
			Assert.IsNull(Behavior.FindPath("c", "a"));
		}
	}

	[TestClass]
	public class MigrationPlannerTests
	{
		private TestModel _model;
		private FakeLookup _lookup;

		[TestInitialize]
		public void Setup()
		{
			_model = new TestModel();
			_lookup = new FakeLookup();
		}

		[TestMethod]
		public void RemovedDefinition_IsDroppedAndReferencesToItAreStrippedAndReported()
		{
			var removed = TestModel.Instance(TestModel.RemovedDefinition, null, Guid.NewGuid(), TestModel.Value(Guid.NewGuid(), "String", "prop"));
			var kept = TestModel.Instance(TestModel.ServiceDefinition, null, Guid.NewGuid());
			var service = TestModel.Instance(
				TestModel.ServiceDefinition,
				"new",
				TestModel.ServiceSection,
				TestModel.ListValue(TestModel.ServiceProperties, "Guid", removed.Id.ToString(), kept.Id.ToString()));

			var plan = Plan(smg: new[] { removed, service, kept });

			var removedItem = Item(plan, removed);
			Assert.AreEqual(PlannedAction.Drop, removedItem.Action);
			Assert.AreEqual(1, removedItem.DroppedData.Count);

			var serviceItem = Item(plan, service);
			Assert.AreEqual(PlannedAction.Create, serviceItem.Action);
			CollectionAssert.AreEqual(new List<Guid> { kept.Id }, (List<Guid>)TestModel.GetValue(serviceItem.Desired, TestModel.ServiceProperties));
			StringAssert.Contains(serviceItem.DroppedData.Single().Reason, "not migrated");

			// Referenced instances are written first.
			Assert.IsTrue(plan.Items.IndexOf(Item(plan, kept)) < plan.Items.IndexOf(serviceItem));
		}

		[TestMethod]
		public void DanglingReference_IsRemovedAndReportedAsDropped()
		{
			var kept = TestModel.Instance(TestModel.ServiceDefinition, null, Guid.NewGuid());
			Guid deleted = Guid.NewGuid();
			var service = TestModel.Instance(
				TestModel.ServiceDefinition,
				"new",
				TestModel.ServiceSection,
				TestModel.ListValue(TestModel.ServiceProperties, "Guid", deleted.ToString(), kept.Id.ToString()));

			var plan = Plan(smg: new[] { service, kept });

			var serviceItem = Item(plan, service);
			Assert.AreEqual(PlannedAction.Create, serviceItem.Action);
			CollectionAssert.AreEqual(new List<Guid> { kept.Id }, (List<Guid>)TestModel.GetValue(serviceItem.Desired, TestModel.ServiceProperties));
			Assert.AreEqual(deleted.ToString(), serviceItem.DroppedData.Single().Value);
			Assert.AreEqual(0, serviceItem.Warnings.Count);
		}

		[TestMethod]
		public void RemovedField_IsReportedAndTheRestOfTheItemIsImported()
		{
			var service = TestModel.Instance(
				TestModel.ServiceDefinition,
				"new",
				TestModel.ServiceSection,
				TestModel.Value(TestModel.ServiceName, "String", "My service"),
				TestModel.Value(TestModel.RemovedField, "String", "script-a"));

			var item = Item(Plan(smg: new[] { service }), service);

			Assert.AreEqual(PlannedAction.Create, item.Action);
			Assert.AreEqual("My service", TestModel.GetValue(item.Desired, TestModel.ServiceName));
			var dropped = item.DroppedData.Single();
			Assert.AreEqual("Scripts", dropped.Field);
			Assert.AreEqual("script-a", dropped.Value);
		}

		[TestMethod]
		public void LegacyLinks_AreMappedToIsLinkedAndLinkedConsumer()
		{
			var producer = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(TestModel.CpvLabel, "String", "producer"));
			var consumer = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(Cpv.LegacyLinkedConfigurationParameter, "Guid", producer.Id.ToString()));
			var dummyLink = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(Cpv.LegacyLinkedInstanceReference, "String", "Dummy Link"));
			var unresolved = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(Cpv.LegacyLinkedConfigurationParameter, "Guid", Guid.NewGuid().ToString()));

			var plan = Plan(config: new[] { consumer, producer, dummyLink, unresolved });

			var consumerItem = Item(plan, consumer);
			Assert.AreEqual(true, TestModel.GetValue(consumerItem.Desired, Cpv.IsLinked));
			CollectionAssert.AreEqual(new List<Guid> { producer.Id }, (List<Guid>)TestModel.GetValue(consumerItem.Desired, Cpv.LinkedConsumer));
			Assert.AreEqual(0, consumerItem.DroppedData.Count);
			Assert.IsTrue(plan.Items.IndexOf(Item(plan, producer)) < plan.Items.IndexOf(consumerItem));

			Assert.AreEqual(true, TestModel.GetValue(Item(plan, dummyLink).Desired, Cpv.IsLinked));

			var unresolvedItem = Item(plan, unresolved);
			Assert.AreEqual(PlannedAction.Create, unresolvedItem.Action);
			Assert.IsNull(TestModel.GetValue(unresolvedItem.Desired, Cpv.IsLinked));
			StringAssert.Contains(unresolvedItem.DroppedData.Single().Reason, "cannot be recreated");
		}

		[TestMethod]
		public void InvalidValue_FailsOnlyThatItem()
		{
			var invalid = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(TestModel.CpvType, "Int32", "9"));
			var valid = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(TestModel.CpvType, "Int32", "1"));

			var plan = Plan(config: new[] { invalid, valid });

			Assert.AreEqual(PlannedAction.Fail, Item(plan, invalid).Action);
			StringAssert.Contains(Item(plan, invalid).Error, "Type");
			Assert.AreEqual(PlannedAction.Create, Item(plan, valid).Action);
		}

		[TestMethod]
		public void Status_IsReachedThroughTransitions_AndUnknownStatusFails()
		{
			var active = TestModel.Instance(TestModel.ServiceDefinition, "active", TestModel.ServiceSection);
			var unknown = TestModel.Instance(TestModel.ServiceDefinition, "cancelled", TestModel.ServiceSection);
			var unreachable = TestModel.Instance(TestModel.ServiceDefinition, "retired", TestModel.ServiceSection);

			var plan = Plan(smg: new[] { active, unknown, unreachable });

			var activeItem = Item(plan, active);
			Assert.AreEqual("new", activeItem.Desired.StatusId);
			CollectionAssert.AreEqual(new[] { "new_to_designed", "designed_to_active" }, activeItem.Transitions.Select(t => t.Id).ToArray());
			StringAssert.Contains(Item(plan, unknown).Error, "does not exist");
			StringAssert.Contains(Item(plan, unreachable).Error, "no status transition path");
		}

		[TestMethod]
		public void ReRun_SkipsUnchangedItems_AndUpdatesChangedOnesKeepingNewModelOnlyFields()
		{
			var unchanged = TestModel.Instance(TestModel.ServiceDefinition, "new", TestModel.ServiceSection, TestModel.Value(TestModel.ServiceName, "String", "Same"));
			var changed = TestModel.Instance(TestModel.ServiceDefinition, "active", TestModel.ServiceSection, TestModel.Value(TestModel.ServiceName, "String", "Legacy name"));
			_lookup.Existing[new InstanceKey(TestModel.Smg, unchanged.Id)] = Existing(unchanged.Id, "new", "Same", null);
			_lookup.Existing[new InstanceKey(TestModel.Smg, changed.Id)] = Existing(changed.Id, "designed", "Edited on target", "new-model value");

			var plan = Plan(smg: new[] { unchanged, changed });

			Assert.AreEqual(PlannedAction.Unchanged, Item(plan, unchanged).Action);
			var changedItem = Item(plan, changed);
			Assert.AreEqual(PlannedAction.Update, changedItem.Action);
			Assert.AreEqual("Legacy name", TestModel.GetValue(changedItem.Desired, TestModel.ServiceName));
			Assert.AreEqual("new-model value", TestModel.GetValue(changedItem.Desired, TestModel.ServiceNewField));
			CollectionAssert.AreEqual(new[] { "designed_to_active" }, changedItem.Transitions.Select(t => t.Id).ToArray());
		}

		[TestMethod]
		public void ReRun_KeepsLinksThatAlreadyExistOnTheTarget()
		{
			var producer = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(TestModel.CpvLabel, "String", "producer"));
			var consumer = TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section, TestModel.Value(Cpv.LegacyLinkedConfigurationParameter, "Guid", producer.Id.ToString()));
			Guid linkedOnTarget = Guid.NewGuid();

			var section = new Section(new SectionDefinitionID(Cpv.Section) { ModuleId = TestModel.Config }) { ID = new SectionID(Guid.NewGuid()) };
			section.AddOrReplaceFieldValue(new FieldValue(new FieldDescriptorID(Cpv.LinkedConsumer), ValueWrapperFactory.Create(new List<Guid> { linkedOnTarget })));
			var existing = new DomInstance
			{
				ID = new DomInstanceId(consumer.Id) { ModuleId = TestModel.Config },
				DomDefinitionId = new DomDefinitionId(TestModel.CpvDefinition) { ModuleId = TestModel.Config },
			};
			existing.Sections.Add(section);
			_lookup.Existing[new InstanceKey(TestModel.Config, consumer.Id)] = existing;

			var item = Item(Plan(config: new[] { consumer, producer }), consumer);

			Assert.AreEqual(PlannedAction.Update, item.Action);
			CollectionAssert.AreEquivalent(new List<Guid> { linkedOnTarget, producer.Id }, (List<Guid>)TestModel.GetValue(item.Desired, Cpv.LinkedConsumer));
		}

		[TestMethod]
		public void NestedProfileLinks_AreOnlyKeptUnderTheirParentProfile()
		{
			var container = KnownIds.ProfileContainers[0];
			var grandChild = TestModel.Instance(KnownIds.Profile.Definition, null, KnownIds.Profile.Section);
			var child = TestModel.Instance(KnownIds.Profile.Definition, null, KnownIds.Profile.Section, TestModel.ListValue(KnownIds.Profile.NestedProfiles, "Guid", grandChild.Id.ToString()));
			var parent = TestModel.Instance(KnownIds.Profile.Definition, null, KnownIds.Profile.Section, TestModel.ListValue(KnownIds.Profile.NestedProfiles, "Guid", child.Id.ToString()));
			var other = TestModel.Instance(KnownIds.Profile.Definition, null, KnownIds.Profile.Section);

			Func<ImportInstance, ImportInstance> link = profile => TestModel.Instance(container.LinkDefinition, null, container.LinkSection, TestModel.Value(container.LinkProfileField, "Guid", profile.Id.ToString()));
			var parentLink = link(parent);
			var childLink = link(child);
			var grandChildLink = link(grandChild);
			var otherLink = link(other);

			var version = TestModel.Instance(container.Definition, null, container.Section, TestModel.ListValue(container.LinksField, "Guid", parentLink.Id.ToString(), childLink.Id.ToString(), grandChildLink.Id.ToString(), otherLink.Id.ToString()));

			// The child profile is a top-level profile of this version, so its link instance must stay.
			var otherVersion = TestModel.Instance(container.Definition, null, container.Section, TestModel.ListValue(container.LinksField, "Guid", childLink.Id.ToString()));

			var plan = Plan(config: new[] { parent, child, grandChild, other }, smg: new[] { version, otherVersion, parentLink, childLink, grandChildLink, otherLink });

			var versionItem = Item(plan, version);
			CollectionAssert.AreEqual(new List<Guid> { parentLink.Id, otherLink.Id }, (List<Guid>)TestModel.GetValue(versionItem.Desired, container.LinksField));
			Assert.AreEqual(2, versionItem.DroppedData.Count);
			StringAssert.Contains(versionItem.DroppedData[0].Reason, "nested under profile");

			CollectionAssert.AreEqual(new List<Guid> { childLink.Id }, (List<Guid>)TestModel.GetValue(Item(plan, otherVersion).Desired, container.LinksField));
			Assert.AreEqual(PlannedAction.Drop, Item(plan, grandChildLink).Action);
			Assert.AreEqual(PlannedAction.Create, Item(plan, childLink).Action);
			CollectionAssert.AreEqual(new List<Guid> { child.Id }, (List<Guid>)TestModel.GetValue(Item(plan, parent).Desired, KnownIds.Profile.NestedProfiles));
		}

		[TestMethod]
		public void LegacyValuesLeftOnTheTarget_AreNotKeptAndDoNotCauseAnUpdate()
		{
			var instance = TestModel.Instance(TestModel.ServiceDefinition, "new", TestModel.ServiceSection, TestModel.Value(TestModel.ServiceName, "String", "Same"));
			var existing = Existing(instance.Id, "new", "Same", null);
			var legacySection = new Section(new SectionDefinitionID(KnownIds.RemovedSections.Keys.First()) { ModuleId = TestModel.Smg }) { ID = new SectionID(Guid.NewGuid()) };
			legacySection.AddOrReplaceFieldValue(new FieldValue(new FieldDescriptorID(Guid.NewGuid()), ValueWrapperFactory.Create("legacy")));
			existing.Sections.Add(legacySection);
			existing.Sections[0].AddOrReplaceFieldValue(new FieldValue(new FieldDescriptorID(KnownIds.RemovedFields.Keys.First()), ValueWrapperFactory.Create(Guid.NewGuid())));
			_lookup.Existing[new InstanceKey(TestModel.Smg, instance.Id)] = existing;

			var item = Item(Plan(smg: new[] { instance }), instance);

			Assert.AreEqual(PlannedAction.Unchanged, item.Action);
			Assert.IsFalse(item.Desired.Sections.Any(s => KnownIds.RemovedSections.ContainsKey(s.SectionDefinitionID.Id)));
			Assert.IsNull(TestModel.GetValue(item.Desired, KnownIds.RemovedFields.Keys.First()));
		}

		[TestMethod]
		public void MissingTargetModule_AbortsTheWholeImport()
		{
			_model.Schemas[TestModel.Config] = new TargetModuleSchema(TestModel.Config, null, null, null);

			var plan = Plan(config: new[] { TestModel.Instance(TestModel.CpvDefinition, null, Cpv.Section) });

			StringAssert.Contains(plan.FatalErrors.Single(), "not installed");
			Assert.AreEqual(0, plan.Items.Count);
		}

		internal static DomInstance Existing(Guid id, string status, string name, string newField)
		{
			var section = new Section(new SectionDefinitionID(TestModel.ServiceSection) { ModuleId = TestModel.Smg }) { ID = new SectionID(Guid.NewGuid()) };
			section.AddOrReplaceFieldValue(new FieldValue(new FieldDescriptorID(TestModel.ServiceName), ValueWrapperFactory.Create(name)));
			if (newField != null)
			{
				section.AddOrReplaceFieldValue(new FieldValue(new FieldDescriptorID(TestModel.ServiceNewField), ValueWrapperFactory.Create(newField)));
			}

			var instance = new DomInstance
			{
				ID = new DomInstanceId(id) { ModuleId = TestModel.Smg },
				DomDefinitionId = new DomDefinitionId(TestModel.ServiceDefinition) { ModuleId = TestModel.Smg },
				StatusId = status,
			};
			instance.Sections.Add(section);
			return instance;
		}

		private static PlannedItem Item(MigrationPlan plan, ImportInstance instance) => plan.Items.Single(i => i.InstanceId == instance.Id);

		private MigrationPlan Plan(IEnumerable<ImportInstance> config = null, IEnumerable<ImportInstance> smg = null)
		{
			var export = TestModel.Export(config ?? Enumerable.Empty<ImportInstance>(), smg ?? Enumerable.Empty<ImportInstance>());
			return new MigrationPlanner(_model.Schemas, _lookup, new FakeEnvironment()).Plan(export);
		}
	}

	[TestClass]
	public class MigrationExecutorTests
	{
		[TestMethod]
		public void FailingTransitionOnCreate_RemovesTheCreatedInstance()
		{
			var writer = new FakeWriter { FailOnTransition = "designed_to_active" };
			var instance = TestModel.Instance(TestModel.ServiceDefinition, "active", TestModel.ServiceSection, TestModel.Value(TestModel.ServiceName, "String", "x"));

			var report = Execute(writer, instance, null, dryRun: false);

			var result = report.Items.Single();
			Assert.AreEqual(ItemOutcome.Failed, result.Outcome);
			StringAssert.Contains(result.Message, "rollback");
			Assert.AreEqual("Delete " + instance.Id, writer.Calls.Last());
		}

		[TestMethod]
		public void FailingTransitionOnUpdate_RestoresThePreviousValues()
		{
			var writer = new FakeWriter { FailOnTransition = "designed_to_active" };
			var instance = TestModel.Instance(TestModel.ServiceDefinition, "active", TestModel.ServiceSection, TestModel.Value(TestModel.ServiceName, "String", "Legacy"));
			var existing = MigrationPlannerTests.Existing(instance.Id, "new", "Original", null);

			var report = Execute(writer, instance, existing, dryRun: false);

			Assert.AreEqual(ItemOutcome.Failed, report.Items.Single().Outcome);
			StringAssert.Contains(report.Items.Single().Message, "stays in status 'designed'");
			Assert.AreEqual($"Update {instance.Id} designed Original", writer.Calls.Last());
		}

		[TestMethod]
		public void DryRun_WritesNothing()
		{
			var writer = new FakeWriter();
			var instance = TestModel.Instance(TestModel.ServiceDefinition, "active", TestModel.ServiceSection);

			var report = Execute(writer, instance, null, dryRun: true);

			Assert.AreEqual(ItemOutcome.Created, report.Items.Single().Outcome);
			StringAssert.Contains(report.BuildSummary(), "DRY RUN");
			Assert.AreEqual(0, writer.Calls.Count);
		}

		private static MigrationReport Execute(FakeWriter writer, ImportInstance instance, DomInstance existing, bool dryRun)
		{
			var lookup = new FakeLookup();
			if (existing != null)
			{
				lookup.Existing[new InstanceKey(TestModel.Smg, instance.Id)] = existing;
			}

			var export = TestModel.Export(Enumerable.Empty<ImportInstance>(), new[] { instance });
			var plan = new MigrationPlanner(new TestModel().Schemas, lookup, new FakeEnvironment()).Plan(export);
			return new MigrationExecutor(writer, null).Execute(plan, dryRun);
		}
	}
}
