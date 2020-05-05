using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XNode {
    /// <summary> Base class for all node graphs </summary>
    [Serializable]
    public abstract class NodeGraph : Node {

        /// <summary> All nodes in the graph. <para/>

        [SerializeField] [HideInInspector] public List<Node> nodes = new List<Node>();
        [SerializeField] public IEnumerable<NodePort> GraphPorts {
            get {
                foreach (var node in nodes)
                {
                    foreach (var port in node.Ports)
                    {
                        if (port.IsAddedToGraph(this)) yield return port;
                    }
                }
            }
        }

        /// <summary> Add a node to the graph by type (convenience method - will call the System.Type version) </summary>
        public T AddNode<T>() where T : Node {
            return AddNode(typeof(T)) as T;
        }

        /// <summary> Add a node to the graph by type </summary>
        public virtual Node AddNode(Type type) {
            Node.graphHotfix = this;
            Node node = ScriptableObject.CreateInstance(type) as Node;
            node.graph = this;
            nodes.Add(node);
            return node;
        }

        public virtual void AddNode(Node node)
        {
            Node.graphHotfix = this;
            node.graph = this;
            nodes.Add(node);
        }

        /// <summary> Creates a copy of the original node in the graph </summary>
        public virtual Node CopyNode(Node original) {
            Node.graphHotfix = this;
            Node node = ScriptableObject.Instantiate(original);
            node.graph = this;
            node.ClearConnections();
            nodes.Add(node);
            return node;
        }

        /// <summary> Safely remove a node and all its connections </summary>
        /// <param name="node"> The node to remove </param>
        public virtual void RemoveNode(Node node) {
            node.ClearConnections();
            nodes.Remove(node);
            if (Application.isPlaying) Destroy(node);
        }

        /// <summary> Remove all nodes and connections from the graph </summary>
        public virtual void Clear() {
            if (Application.isPlaying) {
                for (int i = 0; i < nodes.Count; i++) {
                    Destroy(nodes[i]);
                }
            }
            nodes.Clear();
        }

        /// <summary> Create a new deep copy of this graph </summary>
        public virtual XNode.NodeGraph Copy() {
            // Instantiate a new nodegraph instance
            NodeGraph graph = Instantiate(this);
            // Instantiate all nodes inside the graph
            for (int i = 0; i < nodes.Count; i++) {
                if (nodes[i] == null) continue;
                Node.graphHotfix = graph;
                Node node;
                if (typeof(NodeGraph).IsInstanceOfType(nodes[i]))
                {
                    node = (nodes[i] as NodeGraph).Copy();
                }
                else
                {
                    node = Instantiate(nodes[i]) as Node;
                }
                node.graph = graph;
                graph.nodes[i] = node;
            }
            // Redirect all connections
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] == null) continue;
                foreach (NodePort port in graph.nodes[i].Ports)
                {
                    port.Redirect(this, graph);
                }
            }
            return graph;
        }

        public override object GetValue(NodePort nodePort)
        {
            return nodePort.node.GetValue(nodePort);
        }

        protected virtual void OnDestroy() {
            // Remove all nodes prior to graph destruction
            Clear();
        }
    }
}