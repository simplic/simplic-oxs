using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Simplic.OxS.Data.Test
{
    public class MapServiceTest
    {
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
