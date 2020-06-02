using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestAllocation
{
    public class GraphRoot
    {
        private static int _id;
        public GraphRoot()
        {
            var id = Interlocked.Increment(ref _id);
            Name = this.GetType().Name + id;

            for (int i = 0; i < 2; i++)
                Child.Add(new Child1());
        }

        private byte[] Data = new byte[10240];

        public string Name { get; set; }
        public IList<Child1> Child { get; private set; } = new List<Child1>();
    }

    public class Child1
    {
        private static int _id;
        public Child1()
        {
            var id = Interlocked.Increment(ref _id);
            Name = this.GetType().Name + id;

            for (int i = 0; i < 2; i++)
                GrandChild.Add(new GrandChild1());
        }
        public string Name { get; set; }

        public IList<GrandChild1> GrandChild { get; private set; } = new List<GrandChild1>();
    }

    public class GrandChild1
    {
        private static int _id;
        public GrandChild1()
        {
            var id = Interlocked.Increment(ref _id);
            Name = this.GetType().Name + id;
        }
        public string Name { get; set; }
    }

}
