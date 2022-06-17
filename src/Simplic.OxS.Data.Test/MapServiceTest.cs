using FluentAssertions;
using Xunit;

namespace Simplic.OxS.Data.Test
{
    /// <summary>
    /// Contains MapService tests.
    /// </summary>
    public class MapServiceTest
    {
        /// <summary>
        /// Test Create and TakeDefaults method.
        /// </summary>
        [Fact]
        public void Create_TakeDefaults_SampleObject()
        {
            var mapped = new SampleObject();
            var origin = new SampleObject { CreateUser = "test" };

            var service = new MapService();

            var l = service.Create(origin, mapped);
            
            l.TakeDefaults();

            mapped.CreateUser.Should().Be(origin.CreateUser);
        }

        /// <summary>
        /// Test Create and Take method.
        /// </summary>
        [Fact]
        public void Create_Take_SampleObject()
        {
            var mapped = new SampleObject();
            var origin = new SampleObject { Number = 1 };

            var service = new MapService();

            var l = service.Create(origin, mapped);

            l.Take(x => x.Number);

            mapped.Number.Should().Be(origin.Number);
        }
    }
}
