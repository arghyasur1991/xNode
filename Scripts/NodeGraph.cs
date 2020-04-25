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
        [SerializeField] [HideInInspector] private GraphPortMapDictionary portMap = new GraphPortMapDictionary();

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
                Node node = Instantiate(nodes[i]) as Node;
                node.graph = graph;
                graph.nodes[i] = node;
            }
            // Redirect all connections
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] == null) continue;
                foreach (NodePort port in graph.nodes[i].Ports)
                {
                    port.Redirect(nodes, graph.nodes);
                    NodePort temp = new NodePort(port, nodes[i]);
                    if (!port.IsConnected)
                    {
                        if (port.direction == NodePort.IO.Input)
                        {
                            bool result = graph.portMap.TryGetValue(temp, out NodePort inputPort);
                            if (result)
                            {
                                graph.RemoveDynamicPort(inputPort);
                                graph.portMap.Remove(temp);
                                graph.portMap.Add(port, graph.AddDynamicInput(port.ValueType, port.connectionType, port.typeConstraint, port.fieldName));
                            }
                        }
                        else
                        {
                            foreach (var key in graph.portMap.Keys.ToList())
                            {
                                if (graph.portMap.Comparer.Equals(graph.portMap[key], temp))
                                {
                                    graph.RemoveDynamicPort(key);
                                    graph.portMap.Remove(key);
                                    graph.portMap.Add(graph.AddDynamicOutput(port.ValueType, port.connectionType, port.typeConstraint, port.fieldName), port);
                                }
                            }
                        }
                    }
                }
            }
            graph.portMap.OnBeforeSerialize();
            return graph;
        }

        public T GetInputValue<T>(NodePort nodePort)
        {
            bool result = portMap.TryGetValue(nodePort, out NodePort inputPort);
            if (result)
            {
                return GetInputValue<T>(inputPort.fieldName);
            }
            return default;
        }

        public override object GetValue(NodePort nodePort)
        {
            bool result = portMap.TryGetValue(nodePort, out NodePort outputPort);
            if (result)
            {
                return outputPort.node.GetValue(outputPort);
            }
            return null;
        }

        public void AddFromChildNodePort(NodePort nodePort)
        {
            if (portMap.ContainsKey(nodePort))
            {
                return;
            }
            if (nodePort.IsInput)
            {
                portMap.Add(nodePort, AddDynamicInput(nodePort.ValueType, nodePort.connectionType, nodePort.typeConstraint, nodePort.fieldName));
            }
            else if (nodePort.IsOutput)
            {
                portMap.Add(AddDynamicOutput(nodePort.ValueType, nodePort.connectionType, nodePort.typeConstraint, nodePort.fieldName), nodePort);
            }
        }

        protected virtual void OnDestroy() {
            // Remove all nodes prior to graph destruction
            Clear();
        }

        private class GraphPortMapEqualityComparer : IEqualityComparer<NodePort>
        {
            public bool Equals(NodePort x, NodePort y)
            {
                return (x.ValueType == y.ValueType) &&
                        (x.node == y.node) &&
                        (x.direction == y.direction) &&
                        (x.fieldName == y.fieldName);
            }

            public int GetHashCode(NodePort obj)
            {
                return obj.fieldName.GetHashCode();
            }
        }

        [Serializable]
        private class GraphPortMapDictionary : Dictionary<NodePort, NodePort>, ISerializationCallbackReceiver
        {
            [SerializeField] private List<NodePort> keys = new List<NodePort>();
            [SerializeField] private List<NodePort> values = new List<NodePort>();

            public GraphPortMapDictionary()
                :
                base(new GraphPortMapEqualityComparer())
            {
            }

            public void OnBeforeSerialize()
            {
                keys.Clear();
                values.Clear();
                foreach (KeyValuePair<NodePort, NodePort> pair in this)
                {
                    keys.Add(pair.Key);
                    values.Add(pair.Value);
                }
            }

            public void OnAfterDeserialize()
            {
                this.Clear();

                if (keys.Count != values.Count)
                    throw new System.Exception("there are " + keys.Count + " keys and " + values.Count + " values after deserialization. Make sure that both key and value types are serializable.");

                for (int i = 0; i < keys.Count; i++)
                    this.Add(keys[i], values[i]);
            }
        }
    }
}