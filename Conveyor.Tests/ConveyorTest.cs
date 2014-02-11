using NUnit.Framework;
using System;
using NDeproxy;
using Conveyor;

using CND = Conveyor.NDeproxy;

namespace Conveyor.Tests
{
    [TestFixture]
    public class ConveyorTest
    {
        Deproxy deproxy;
        Server conveyor;

        [Test]
        public void TestCase()
        {
            given:
            int port = PortFinder.Singleton.getNextOpenPort();
            deproxy = new Deproxy();
            Boolean passed = false;
            conveyor = new Server(
                port,
                (x) => {
                    passed = true;
                    return new Conveyor.Response(200); } );

            when:
            var mc = deproxy.makeRequest(url: string.Format("http://localhost:{0}", port));

            then:
            Assert.IsTrue(passed);
            Assert.AreEqual("200", mc.receivedResponse.code);
        }

        [TearDown]
        public void TearDown()
        {
            if (deproxy != null)
            {
                deproxy.shutdown();
            }

            if (conveyor != null)
            {
                conveyor.shutdown();
            }
        }
    }
}

