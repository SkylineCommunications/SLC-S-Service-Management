// <copyright file="CounterElementCreationTests.cs" company="Skyline Communications">
// Copyright (c) Skyline Communications. All rights reserved.
// </copyright>

namespace SLC_SM_CommonTests.IdGeneration
{
	using System;
	using System.Collections.Generic;
	using Moq;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using SLC_SM_Common.IdGeneration;

	[TestClass]
	public class CounterElementCreationTests
	{
		private const string CounterName = "Service Management Mediation";
		private const string ProtocolName = "Skyline Service Management Mediation";

		[TestMethod]
		public void Creation_UsesLowestAgentIdRegardlessOfEnumerationOrder()
		{
			var fixture = new Fixture();
			fixture.Owner.SetupSequence(agent => agent.ElementExists(CounterName)).Returns(false).Returns(true);
			fixture.Owner.Setup(agent => agent.CreateElement(It.IsAny<ElementConfiguration>())).Returns(new DmsElementId(1, 10));

			var result = fixture.Find();

			Assert.AreSame(fixture.Element.Object, result);
			fixture.Owner.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Once);
			fixture.Other.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Never);
		}

		[TestMethod]
		public void CompetingCreation_RecoversNamedElementOnOwnerWhenDmsDiscoveryIsStale()
		{
			var fixture = new Fixture();
			fixture.Owner.SetupSequence(agent => agent.ElementExists(CounterName)).Returns(false).Returns(true);
			fixture.Owner.Setup(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()))
				.Throws(new IncorrectDataException("Element name already exists."));

			Assert.AreSame(fixture.Element.Object, fixture.Find());
			Assert.AreEqual(1, fixture.Logs.Count);
			fixture.Other.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Never);
		}

		[TestMethod]
		public void OwnerAlreadyHasCounter_DoesNotAttemptAnotherCreation()
		{
			var fixture = new Fixture();
			fixture.Owner.Setup(agent => agent.ElementExists(CounterName)).Returns(true);

			Assert.AreSame(fixture.Element.Object, fixture.Find());
			fixture.Owner.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Never);
		}

		[TestMethod]
		public void CompetingCreation_WaitsForWinnerToBecomeVisibleOnOwner()
		{
			var fixture = new Fixture();
			fixture.Owner.SetupSequence(agent => agent.ElementExists(CounterName))
				.Returns(false).Returns(false).Returns(false).Returns(true);
			fixture.Owner.Setup(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()))
				.Throws(new IncorrectDataException("Element name already exists."));

			var result = ServiceManagementIdGenerator.FindOrCreateCounterElement(fixture.Dms.Object, fixture.Logs.Add, TimeSpan.FromSeconds(2));

			Assert.AreSame(fixture.Element.Object, result);
			fixture.Owner.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Once);
		}

		[TestMethod]
		public void FailedCreationWithoutWinner_RethrowsOriginalErrorWithoutChangingOwner()
		{
			var fixture = new Fixture();
			var failure = new IncorrectDataException("Creation failed.");
			fixture.Owner.Setup(agent => agent.CreateElement(It.IsAny<ElementConfiguration>())).Throws(failure);

			Assert.AreSame(failure, Assert.ThrowsException<IncorrectDataException>(() => fixture.Find()));
			Assert.AreEqual(0, fixture.Logs.Count);
			fixture.Other.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Never);
		}

		[TestMethod]
		public void NamedElementWithWrongProtocol_IsRejected()
		{
			var fixture = new Fixture();
			fixture.Owner.Setup(agent => agent.ElementExists(CounterName)).Returns(true);
			var otherProtocol = new Mock<IDmsProtocol>();
			otherProtocol.SetupGet(protocol => protocol.Name).Returns("Other connector");
			fixture.Element.SetupGet(element => element.Protocol).Returns(otherProtocol.Object);

			Assert.ThrowsException<InvalidOperationException>(() => fixture.Find());
		}

		[TestMethod]
		public void ExistingDuplicateCounters_FailClosedRatherThanAllocatingFromDifferentElements()
		{
			var fixture = new Fixture();
			fixture.Dms.Setup(dms => dms.GetElements()).Returns(new[] { fixture.Element.Object, fixture.Element.Object });

			Assert.ThrowsException<InvalidOperationException>(() => fixture.Find());
			fixture.Owner.Verify(agent => agent.CreateElement(It.IsAny<ElementConfiguration>()), Times.Never);
		}

		private sealed class Fixture
		{
			public Fixture()
			{
				var protocol = new Mock<IDmsProtocol>();
				protocol.SetupGet(value => value.Name).Returns(ProtocolName);
				protocol.SetupGet(value => value.Version).Returns("Production");
				protocol.SetupGet(value => value.ReferencedVersion).Returns("1.0.0.1");
				protocol.SetupGet(value => value.Type).Returns(ProtocolType.Virtual);
				var connection = new Mock<IDmsConnectionInfo>();
				connection.SetupGet(value => value.Type).Returns(ConnectionType.Virtual);
				protocol.SetupGet(value => value.ConnectionInfo).Returns(new List<IDmsConnectionInfo> { connection.Object });
				this.Element.SetupGet(value => value.Protocol).Returns(protocol.Object);
				this.Element.SetupGet(value => value.State).Returns(ElementState.Active);
				this.Element.Setup(value => value.IsStartupComplete()).Returns(true);
				this.Owner.SetupGet(agent => agent.Id).Returns(1);
				this.Other.SetupGet(agent => agent.Id).Returns(2);
				this.Owner.Setup(agent => agent.GetElement(CounterName)).Returns(this.Element.Object);
				this.Dms.Setup(dms => dms.GetProtocol(ProtocolName, "Production")).Returns(protocol.Object);
				this.Dms.Setup(dms => dms.GetElements()).Returns(Array.Empty<IDmsElement>());
				this.Dms.Setup(dms => dms.GetAgents()).Returns(new[] { this.Other.Object, this.Owner.Object });
			}

			public Mock<IDms> Dms { get; } = new Mock<IDms>();

			public Mock<IDma> Owner { get; } = new Mock<IDma>();

			public Mock<IDma> Other { get; } = new Mock<IDma>();

			public Mock<IDmsElement> Element { get; } = new Mock<IDmsElement>();

			public List<string> Logs { get; } = new List<string>();

			public IDmsElement Find()
			{
				return ServiceManagementIdGenerator.FindOrCreateCounterElement(this.Dms.Object, this.Logs.Add, TimeSpan.Zero);
			}
		}
	}
}
