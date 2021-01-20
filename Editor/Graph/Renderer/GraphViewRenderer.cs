using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GraphVisualizer;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using Edge = UnityEditor.Experimental.GraphView.Edge;
using Node = UnityEditor.Experimental.GraphView.Node;
using Object = UnityEngine.Object;
using Vertex = GraphVisualizer.Vertex;

public class GraphViewRenderer : IGraphRenderer
{
    public void Draw(IGraphLayout graphLayout, Rect drawingArea)
    {
        // doesn't seem to be used anywhere?
        throw new NotImplementedException();
    }

    private GraphView graphView;

    public class PlayableGraphView : GraphView
    {
        public PlayableGraphView()
        {
            SetupZoom(0.1f, ContentZoomer.DefaultMaxScale);
            this.viewTransformChanged = GraphTransformChanged;
            
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            // VisualElement background = new VisualElement
            // {
            //     style =
            //     {
            //         backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f)
            //     }
            // };
            // Insert(0, background);
            style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);

            // background.StretchToParentSize();
            this.StretchToParentSize();

            var path = "Packages/com.unity.playablegraph-visualizer/Editor/Resources/GraphViewRenderer.uss";
#if UNITY_2019_1_OR_NEWER
            this.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
#else
            this.AddStyleSheetPath(path));
#endif
        }

        private string lastZoomClass = "";
        private void GraphTransformChanged(GraphView graphview)
        {
            // Debug.Log(graphview.scale);
            
            // zoom levels every 0.25 steps
            graphview.RemoveFromClassList(lastZoomClass);
            var zoom = Mathf.CeilToInt(graphview.scale / 0.25f) * 25;
            if (graphview.scale < 0.2f)
                zoom = 10;
            lastZoomClass = "__zoom_" + zoom;
            graphview.AddToClassList(lastZoomClass);
        }
    }

    private IGraphLayout graphLayout;
    public void Draw(IGraphLayout graphLayout, Rect drawingArea, GraphSettings graphSettings)
    {
        this.graphLayout = graphLayout;
        if (this.graphLayout == null) return;
        
        foreach (var o in graphLayout.edges)
        {
            // find matching edge in dictionary so we can update the weights properly
            if (edgeToEdge.TryGetValue((o.destination.node.content, o.source.node.content), out var kvp))
            {
                foreach(var edge in kvp)
                    edge.style.opacity = o.source.node.weight * 0.8f + 0.2f;
            }
            // var graphEdge = edgeToEdge.FirstOrDefault(x => x.Key.destination.node == o.destination.node && x.Key.source.node == o.source.node).Value;
            // if(graphEdge != null)
            //     graphEdge.style.co
        }
    }

    Dictionary<Vertex, Node> vertexToNode = new Dictionary<Vertex, Node>();
    private Dictionary<(object, object), List<Edge>> edgeToEdge = new Dictionary<(object, object), List<Edge>>();
    
    public void RedrawNodes()
    {
        // update the visual element
        foreach (var e in graphView.edges.ToList())
            graphView.RemoveElement(e);
        foreach(var n in graphView.nodes.ToList())
            graphView.RemoveElement(n);
        
        if (graphLayout == null) return;
    
        vertexToNode.Clear();
        foreach(var v in graphLayout.vertices)
        {
            var newNode = MakeNode(v.node);
            graphView.AddElement(newNode);
            newNode.SetPosition(new Rect(v.position * 200, new Vector2(0,0)));
            vertexToNode.Add(v, newNode);
        }

        edgeToEdge.Clear();
        foreach (var d in graphLayout.edges)
        {
            var input = vertexToNode[d.source];
            var output = vertexToNode[d.destination];
            
            Edge edge = new Edge
            {
                input = input.inputContainer[0] as Port,
                output = output.outputContainer[0] as Port,
            };
            edge.style.opacity = d.source.node.weight * 0.8f + 0.2f;
            edge.input?.Connect(edge);
            edge.output?.Connect(edge);

            input.RefreshPorts();
            output.RefreshPorts();
            
            graphView.AddElement(edge);
            var tuple = (d.destination.node.content, d.source.node.content);
            if (!edgeToEdge.ContainsKey(tuple))
                edgeToEdge.Add(tuple, new List<Edge>());
            edgeToEdge[tuple].Add(edge);
        }

        Debug.Log("Added " + graphView.nodes.ToList().Count + " nodes, " + graphView.edges.ToList().Count + " edges.");

        void FrameAndForget(GeometryChangedEvent evt)
        {
            graphView.FrameAll();
            graphView.UnregisterCallback<GeometryChangedEvent>(FrameAndForget);
        }

        graphView.RegisterCallback<GeometryChangedEvent>(FrameAndForget);
    }
    
    private const float kNodeWidth = 200.0f;
    Node MakeNode(GraphVisualizer.Node graphNode)
    {
        var nodeName = ObjectNames.NicifyVariableName(graphNode.GetContentType().Name);
        
        var objNode = new Node
        {
            title = nodeName,
            style =
            {
                width = kNodeWidth
            }
        };

        objNode.titleContainer.style.borderBottomColor = graphNode.GetColor();
        // objNode.titleContainer.style.borderBottomWidth = 8;

        objNode.extensionContainer.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 0.8f);

        // objNode.titleContainer.Add(new Button(() =>
        // {
        //     Selection.activeObject = pingObject;
        //     EditorGUIUtility.PingObject(pingObject);
        // })
        // {
        //     style =
        //     {
        //         height = 16.0f,
        //         alignSelf = Align.Center,
        //         alignItems = Align.Center
        //     },
        //     text = "Select"
        // });

        var infoContainer = new VisualElement
        {
            style =
            {
                paddingBottom = 4.0f,
                paddingTop = 4.0f,
                paddingLeft = 4.0f,
                paddingRight = 4.0f
            }
        };

        infoContainer.name = "InfoContainer";

        infoContainer.Add(new Button(() =>
        { 
            Debug.Log(graphNode.ToString() + "\nweight: " + graphNode.weight + "\nactive: " + graphNode.active + "\ncolor: " + graphNode.GetColor() + "\ntype: " + graphNode.GetContentTypeName());
        })
        {
            text = "Log Info",
        });
        objNode.extensionContainer.Add(infoContainer);

        Port realPort = objNode.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single,
            typeof(Object));
        realPort.portName = "In";
        objNode.inputContainer.Add(realPort);

#if UNITY_2018_1
        Port port = objNode.InstantiatePort(Orientation.Horizontal, Direction.Output, typeof(Object));
#else
        Port port = objNode.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single,
            typeof(Object));
#endif
        port.portName = "Out";
        objNode.outputContainer.Add(port);
        objNode.RefreshPorts();

        objNode.RefreshExpandedState();
        objNode.RefreshPorts();
        objNode.capabilities &= ~Capabilities.Deletable;
        objNode.capabilities |= Capabilities.Collapsible;

        return objNode;
    }

    public VisualElement GetVisualElement()
    {
        
        if (graphView == null) {
            graphView = new PlayableGraphView
            {
                name = "Playable Graph View",
                style = { flexDirection = FlexDirection.Row, flexGrow = 1}
            };
        }
        
        var toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 0,
                backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.75f)
            }
        };
        toolbar.Add(new Button(RedrawNodes)
        {
            text = "Recreate Nodes",
        });

        toolbar.Add(new Button(FrameAll)
        {
            text = "Frame All",
        });

        var v = new VisualElement();
        v.Add(graphView);
        toolbar.style.position = Position.Absolute;
        v.Add(toolbar);
        
        v.StretchToParentSize();
        v.style.marginTop = 22;

        return v;
    }

    private void FrameAll()
    {
        graphView.FrameAll();
    }
}
