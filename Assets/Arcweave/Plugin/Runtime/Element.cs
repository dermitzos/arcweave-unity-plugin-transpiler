using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Element : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string title { get; private set; }
        [field: SerializeField]
        public string rawContent { get; private set; }
        [field: SerializeReference]
        public List<Component> components { get; private set; }
        [field: SerializeField]
        public Cover cover { get; private set; }
        [field: SerializeField]
        public List<Connection> outputs { get; private set; }

        public Project project { get; private set; }
        private System.Func<Project, string> runtimeContentFunc { get; set; }

        void INode.InitializeInProject(Project project) { this.project = project; }
        Path INode.ResolvePath(Path p) {
            if ( string.IsNullOrEmpty(p.label) ) { p.label = title; }
            p.targetElement = this;
            return p;
        }

        internal void Set(string id, List<Connection> outputs, string title, string content, List<Component> components, Cover cover) {
            this.id = id;
            this.outputs = outputs;
            this.title = title;
            this.rawContent = content;
            this.components = components;
            this.cover = cover;
        }

        ///----------------------------------------------------------------------------------------------

        ///Returns the runtime content taking into account and executing arcscript
        public string GetRuntimeContent() {
            if ( runtimeContentFunc == null ) {
                var methodName = "Element_" + id.Replace("-", "_").ToString();
                var methodInfo = typeof(ArcscriptImplementations).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Debug.Assert(methodInfo != null);
                runtimeContentFunc = (System.Func<Project, string>)System.Delegate.CreateDelegate(typeof(System.Func<Project, string>), null, methodInfo);
            }
            return Utils.CleanString(runtimeContentFunc(project));
        }

        ///----------------------------------------------------------------------------------------------

        ///Represents the state of the element with possible paths to next elements taking into account conditions, invalid jumper links, etc.
        public State GetState() => new State(this);

        public bool HasContent() => !string.IsNullOrEmpty(rawContent);
        public bool HasComponent(string name) => TryGetComponent(name, out var component);
        public bool TryGetComponent(string name, out Component component) {
            component = components.FirstOrDefault(x => x.name == name);
            return component != null;
        }

        ///Returns a Texture2D asset by the same image name as the one used in Arcweave and loaded from a "Resources" asset folder.
        public Texture2D GetCoverImage() => cover?.ResolveImage();
        ///Returns a Texture2D asset of the first component attached to the element by the same image name as the one used in Arcweave and loaded from a "Resources" asset folder.
        public Texture2D GetFirstComponentCoverImage() => components != null && components.Count > 0 ? components[0].GetCoverImage() : null;
    }
}