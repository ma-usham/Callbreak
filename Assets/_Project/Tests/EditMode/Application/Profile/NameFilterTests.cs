using Callbreak.Application.Profile;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>The display-name block list: case, spacing, leetspeak and accents, without catching real names.</summary>
    public sealed class NameFilterTests
    {
        private NameFilter _filter;

        [SetUp]
        public void SetUp()
        {
            _filter = new NameFilter();
        }

        [TestCase("Fuck")]
        [TestCase("FUCK")]
        [TestCase("f u c k")]
        [TestCase("Fück")]
        [TestCase("Mr Fuckface")]
        [TestCase("B1tch")]
        [TestCase("5h1t")]
        [TestCase("Sh1t Head")]
        [TestCase("ass")]
        [TestCase("a s s")]
        [TestCase("Big Ass")]
        [TestCase("sexy boy")]
        [TestCase("Hitler")]
        [TestCase("Muji")]
        [TestCase("Randi Ko Ban")]
        [TestCase("Machikne")]
        [TestCase("M4d4rch0d")]
        [TestCase("Chutiya")]
        [TestCase("Mujiko")]
        [TestCase("Bhen Chod")]
        public void Blocks(string name)
        {
            Assert.IsFalse(_filter.IsAllowed(name));
        }

        [TestCase("Player")]
        [TestCase("Kshitij")]
        [TestCase("Shital")]
        [TestCase("Hassan")]
        [TestCase("Titiksha")]
        [TestCase("Dickson")]
        [TestCase("Cassandra")]
        [TestCase("Assam")]
        [TestCase("Arsenal Fan")]
        [TestCase("Sexton")]
        [TestCase("Milford")]
        [TestCase("Fukuda")]
        [TestCase("Randip")]
        [TestCase("Pasang Lhamu")]
        [TestCase("Bikash")]
        [TestCase("Asha")]
        [TestCase("Suman")]
        [TestCase("Basanta")]
        [TestCase("Sushma")]
        [TestCase("Prashant 77")]
        [TestCase("राम")]
        [TestCase("")]
        [TestCase(null)]
        public void Allows(string name)
        {
            Assert.IsTrue(_filter.IsAllowed(name));
        }

        [Test]
        public void Fold_LowerCasesMapsLeetspeakAndDropsLatinAccents()
        {
            Assert.AreEqual("oieaststuff", NameFilter.Fold("013457Stüff"));
            Assert.AreEqual("a b", NameFilter.Fold("A\tB"));
            Assert.AreEqual("राम", NameFilter.Fold("राम"), "other scripts keep their marks");
        }
    }
}
