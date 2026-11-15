using System;
using System.Collections;
namespace NUnit.Framework
{
    public static class CollectionAssert { public static void AreEqual(IEnumerable a, IEnumerable b) { } }
    public static class Is2 { }
}
namespace UnityEngine.InputSystem
{
    public class InputTestFixture
    {
        public virtual void Setup() { }
        public virtual void TearDown() { }
        protected void Press(UnityEngine.InputSystem.Controls.ButtonControl c) { }
        protected void Release(UnityEngine.InputSystem.Controls.ButtonControl c) { }
    }
}
