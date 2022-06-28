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
            var origin = new SampleObject { CreateUser = "test", Id = 2, Number = 1 };

            var service = new MapService();

            var l = service.Create(origin, mapped);
            
            l.TakeDefaults<SampleObject, ISampleDefaultDocument>();

            mapped.CreateUser.Should().Be(origin.CreateUser);
            mapped.Id.Should().Be(origin.Id);
            mapped.Number.Should().NotBe(origin.Number);
        }

        /// <summary>
        /// Test Create and Take method.
        /// </summary>
        [Fact]
        public void Create_Take_SampleObject()
        {
            var mapped = new SampleObject();
            var origin = new SampleObject { Id = 2, Number = 1 };

            var service = new MapService();

            var l = service.Create(origin, mapped);

            l.Take(x => x.Number);

            mapped.Number.Should().Be(origin.Number);
            mapped.Id.Should().NotBe(origin.Number);
        }
    }
}
