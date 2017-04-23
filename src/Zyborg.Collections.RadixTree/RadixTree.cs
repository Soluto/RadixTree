using System;
using System.Collections.Generic;
using System.IO;

namespace Zyborg.Collections
{
    // WalkFn is used when walking the tree. Takes a
    // key and value, returning if iteration should
    // be terminated.
    //~ type WalkFn func(s string, v interface{ }) bool
    public delegate bool Walker<TValue>(string key, TValue value);

    //// leafNode is used to represent a value
    //~ type leafNode struct {
    //~ 	key string
    //~ 	val interface{}
    //~ }
    internal class LeafNode<TValue>
    {
        internal string _key = string.Empty;
        internal TValue _value;
    }

    //// edge is used to represent an edge node
    //~ type edge struct {
    //~ 	label byte
    //~ 	node  *node
    //~ }
    internal class Edge<TValue>
    {
        internal char _label;
        internal Node<TValue> _node;
    }

    //~ type node struct {
    //~ 	// leaf is used to store possible leaf
    //~ 	leaf *leafNode
    //~ 
    //~ 	// prefix is the common prefix we ignore
    //~ 	prefix string
    //~ 
    //~ 	// Edges should be stored in-order for iteration.
    //~ 	// We avoid a fully materialized slice to save memory,
    //~ 	// since in most cases we expect to be sparse
    //~ 	edges edges
    //~ }
    internal class Node<TValue>
    {
        internal LeafNode<TValue> _leaf;
        internal string _prefix = string.Empty;
        internal SortedList<char, Edge<TValue>> _edges = new SortedList<char, Edge<TValue>>();

        //~ func (n *node) isLeaf() bool {
        //~ 	return n.leaf != nil
        //~ }
        public bool IsLeaf => _leaf != null;

        //~ func (n *node) addEdge(e edge) {
        //~ 	n.edges = append(n.edges, e)
        //~ 	n.edges.Sort()
        //~ }
        public void AddEdge(Edge<TValue> e)
        {
            _edges.Add(e._label, e);
        }

        //~ func (n *node) replaceEdge(e edge) {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= e.label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == e.label {
        //~ 		n.edges[idx].node = e.node
        //~ 		return
        //~ 	}
        //~ 	panic("replacing missing edge")
        //~ }
        public void ReplaceEdge(Edge<TValue> e)
        {
            if (!_edges.TryGetValue(e._label, out var edge)) throw new Exception("replacing missing edge");

            edge._node = e._node;
        }

        //~ func (n *node) getEdge(label byte) *node {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == label {
        //~ 		return n.edges[idx].node
        //~ 	}
        //~ 	return nil
        //~ }
        public Node<TValue> GetEdge(char label)
        {
          return _edges.TryGetValue(label, out var edge) ? edge._node : null;
        } 

        //~ func (n *node) delEdge(label byte) {
        //~ 	num := len(n.edges)
        //~ 	idx := sort.Search(num, func(i int) bool {
        //~ 		return n.edges[i].label >= label
        //~ 	})
        //~ 	if idx < num && n.edges[idx].label == label {
        //~ 		copy(n.edges[idx:], n.edges[idx+1:])
        //~ 		n.edges[len(n.edges)-1] = edge{}
        //~ 		n.edges = n.edges[:len(n.edges)-1]
        //~ 	}
        //~ }
        public void RemoveEdge(char label)
        {
            _edges.Remove(label);
        }

        //~ func (n *node) mergeChild() {
        //~ 	e := n.edges[0]
        //~ 	child := e.node
        //~ 	n.prefix = n.prefix + child.prefix
        //~ 	n.leaf = child.leaf
        //~ 	n.edges = child.edges
        //~ }
        public void MergeChild()
        {
            var e = this._edges.Values[0];
            var child = e._node;

            this._prefix = this._prefix + child._prefix;
            this._leaf = child._leaf;
            this._edges = child._edges;
        }

        public void Print(StreamWriter w, string prefix)
        {
            w.WriteLine($"{prefix}Prfx=[{this._prefix}]");
            if (this._leaf != null)
                w.WriteLine($"{prefix}Leaf:  [{this._leaf._key}] = [{this._leaf._value}]");
            foreach (var e in _edges.Values)
            {
                w.WriteLine($"{prefix}Edge:  label=[{e._label}]");
                e._node.Print(w, prefix + "  ");
            }
        }
    }

    // Tree implements a radix tree. This can be treated as a
    // Dictionary abstract data type. The main advantage over
    // a standard hash map is prefix-based lookups and
    // ordered iteration,
    //~ type Tree struct {
    //~ 	root *node
    //~ 	size int
    //~ }
    public class RadixTree<TValue>
    {
        private readonly Node<TValue> _root;
        private int _size;

        // New returns an empty Tree
        public RadixTree()
            : this(null)
        { }

        // NewFromMap returns a new tree containing the keys
        // from an existing map
        //~ func NewFromMap(m map[string]interface{}) *Tree {
        //~ 	t := &Tree{root: &node{}}
        //~ 	for k, v := range m {
        //~ 		t.Insert(k, v)
        //~ 	}
        //~ 	return t
        //~ }
        public RadixTree(IReadOnlyDictionary<string, TValue> m)
        {
            _root = new Node<TValue>();
            if (m != null)
            {
                foreach (var kv in m)
                {
                    this.GoInsert(kv.Key, kv.Value);
                }
            }
        }

        // Len is used to return the number of elements in the tree
        public int Count => _size;

        // longestPrefix finds the length of the shared prefix
        // of two strings
        public static int FindLongestPrefix(string k1, string k2)
        {
            //	max := len(k1)
            //	if l := len(k2); l < max {
            //		max = l
            //	}
            //	var i int
            //	for i = 0; i < max; i++ {
            //		if k1[i] != k2[i] {
            //			break
            //		}
            //	}
            //	return i

            var max = k1.Length > k2.Length ? k2.Length : k1.Length;

            for (var i = 0; i < max; i++)
                if (k1[i] != k2[i])
                    return i;

            return max;
        }

        // Insert is used to add a newentry or update
        // an existing entry. Returns if updated.
        //~ func (t *Tree) Insert(s string, v interface{}) (interface{}, bool) {
        public (TValue oldValue, bool updated) GoInsert(string key, TValue value)
        {
            //~ var parent *node
            //~ n := t.root
            //~ search := s
            var n = _root;
            var search = key;

            //~ for {
            while (true)
            {
                // Handle key exhaution
                //~ if len(search) == 0 {
                //~ 	if n.isLeaf() {
                //~ 		old := n.leaf.val
                //~ 		n.leaf.val = v
                //~ 		return old, true
                //~ 	}
                //~
                //~ 	n.leaf = &leafNode{
                //~ 		key: s,
                //~ 		val: v,
                //~ 	}
                //~ 	t.size++
                //~ 	return nil, false
                //~ }
                if (search.Length == 0)
                {
                    if (n.IsLeaf)
                    {
                        var old = n._leaf._value;
                        n._leaf._value = value;
                        return (old, true);
                    }

                    n._leaf = new LeafNode<TValue>
                    {
                        _key = key,
                        _value = value,
                    };
                    _size++;
                    return (default(TValue), false);
                }

                // Look for the edge
                //~ parent = n
                //~ n = n.getEdge(search[0])
                var parent = n;
                n = n.GetEdge(search[0]);

                // No edge, create one
                //~ if n == nil {
                //~ 	e := edge{
                //~ 		label: search[0],
                //~ 		node: &node{
                //~ 			leaf: &leafNode{
                //~ 				key: s,
                //~ 				val: v,
                //~ 			},
                //~ 			prefix: search,
                //~ 		},
                //~ 	}
                //~ 	parent.addEdge(e)
                //~ 	t.size++
                //~ 	return nil, false
                //~ }
                if (n == null)
                {
                    var e = new Edge<TValue>
                    {
                        _label = search[0],
                        _node = new Node<TValue>
                        {
                            _leaf = new LeafNode<TValue>
                            {
                                _key = key,
                                _value = value,
                            },
                            _prefix = search,
                        }
                    };
                    parent.AddEdge(e);
                    _size++;
                    return (default(TValue), false);
                }

                // Determine longest prefix of the search key on match
                //~ commonPrefix := longestPrefix(search, n.prefix)
                //~ if commonPrefix == len(n.prefix) {
                //~ 	search = search[commonPrefix:]
                //~ 	continue
                //~ }
                var commonPrefix = FindLongestPrefix(search, n._prefix);
                if (commonPrefix == n._prefix.Length)
                {
                    search = search.Substring(commonPrefix);
                    continue;
                }

                // Split the node
                //~ t.size++
                //~ child := &node{
                //~ 	prefix: search[:commonPrefix],
                //~ }
                //~ parent.replaceEdge(edge{
                //~ 	label: search[0],
                //~ 	node:  child,
                //~ })
                _size++;
                var child = new Node<TValue>
                {
                    _prefix = search.Substring(0, commonPrefix),
                };
                parent.ReplaceEdge(new Edge<TValue>
                {
                    _label = search[0],
                    _node = child,
                });


                // Restore the existing node
                //~ child.addEdge(edge{
                //~ 	label: n.prefix[commonPrefix],
                //~ 	node:  n,
                //~ })
                //~ n.prefix = n.prefix[commonPrefix:]
                child.AddEdge(new Edge<TValue>
                {
                    _label = n._prefix[commonPrefix],
                    _node = n,
                });
                n._prefix = n._prefix.Substring(commonPrefix);

                // Create a new leaf node
                //~ leaf := &leafNode{
                //~ 	key: s,
                //~ 	val: v,
                //~ }
                var leaf = new LeafNode<TValue>
                {
                    _key = key,
                    _value = value,
                };

                // If the new key is a subset, add to to this node
                //~ search = search[commonPrefix:]
                //~ if len(search) == 0 {
                //~ 	child.leaf = leaf
                //~ 	return nil, false
                //~ }
                search = search.Substring(commonPrefix);
                if (search.Length == 0)
                {
                    child._leaf = leaf;
                    return (default(TValue), false);
                }

                // Create a new edge for the node
                //~ child.addEdge(edge{
                //~ 	label: search[0],
                //~ 	node: &node{
                //~ 		leaf:   leaf,
                //~ 		prefix: search,
                //~ 	},
                //~ })
                //~ return nil, false
                child.AddEdge(new Edge<TValue>
                {
                    _label = search[0],
                    _node = new Node<TValue>
                    {
                        _leaf = leaf,
                        _prefix = search,
                    }
                });
                return (default(TValue), false);
            }
        }

        // Delete is used to delete a key, returning the previous
        // value and if it was deleted
        //!func (t *Tree) Delete(s string) (interface{}, bool) {
        public (TValue oldValue, bool deleted) GoDelete(string s)
        {
            //~	var parent *node
            //~	var label byte
            //~	n := t.root
            //~	search := s
            Node<TValue> parent = null;
            char label = char.MinValue;
            var n = _root;
            var search = s;

            //!	for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	if !n.isLeaf() {
                //~ 		break
                //~ 	}
                //~ 	goto DELETE
                //~ }
                if (search.Length == 0)
                {
                    if (!n.IsLeaf)
                        break;

                    goto DELETE;
                }

                // Look for an edge
                //~ parent = n
                //~ label = search[0]
                //~ n = n.getEdge(label)
                //~ if n == nil {
                //~ 	break
                //~ }
                parent = n;
                label = search[0];
                n = n.GetEdge(label);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n._prefix))
                    search = search.Substring(n._prefix.Length);
                else
                    break;
            }

            //~	return nil, false
            return (default(TValue), false);

            DELETE:
            // Delete the leaf
            //~	leaf := n.leaf
            //~	n.leaf = nil
            //~	t.size--
            var leaf = n._leaf;
            n._leaf = null;
            _size--;

            // Check if we should delete this node from the parent
            //~	if parent != nil && len(n.edges) == 0 {
            //~		parent.delEdge(label)
            //~	}
            if (parent != null && n._edges.Count == 0)
                parent.RemoveEdge(label);

            // Check if we should merge this node
            //~	if n != t.root && len(n.edges) == 1 {
            //~		n.mergeChild()
            //~	}
            if (n != _root && n._edges.Count == 1)
                n.MergeChild();

            // Check if we should merge the parent's other child
            //~	if parent != nil && parent != t.root && len(parent.edges) == 1 && !parent.isLeaf() {
            //~		parent.mergeChild()
            //~	}
            if (parent != null && parent != _root && parent._edges.Count == 1 && !parent.IsLeaf)
                parent.MergeChild();

            //~ return leaf.val, true
            return (leaf._value, true);
        }

        // Get is used to lookup a specific key, returning
        // the value and if it was found
        //~ func (t *Tree) Get(s string) (interface{}, bool) {
        public (TValue value, bool found) GoGet(string s)
        {
            //	n := t.root
            //	search := s
            var n = _root;
            var search = s;

            //~ for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	if n.isLeaf() {
                //~ 		return n.leaf.val, true
                //~ 	}
                //~ 	break
                //~ }
                if (search.Length == 0)
                {
                    if (n.IsLeaf)
                        return (n._leaf._value, true);
                    break;
                }

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                var oldN = n;
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n._prefix))
                    search = search.Substring(n._prefix.Length);
                else
                    break;
            }

            //~	return nil, false
            return (default(TValue), false);
        }

        // LongestPrefix is like Get, but instead of an
        // exact match, it will return the longest prefix match.
        //~ func (t *Tree) LongestPrefix(s string) (string, interface{}, bool) {
        public (string key, TValue value, bool found) LongestPrefix(string s)
        {
            //~ var last *leafNode
            //~ n := t.root
            //~ search := s
            LeafNode<TValue> last = null;
            var n = _root;
            var search = s;

            //for {
            while (true)
            {
                // Look for a leaf node
                //~ if n.isLeaf() {
                //~ 	last = n.leaf
                //~ }
                if (n.IsLeaf)
                    last = n._leaf;

                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	break
                //~ }
                if (search.Length == 0)
                    break;

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n._prefix))
                    search = search.Substring(n._prefix.Length);
                else
                    break;
            }

            //~ if last != nil {
            //~ 	return last.key, last.val, true
            //~ }
            //~ return "", nil, false

            if (last != null)
                return (last._key, last._value, true);
            return (string.Empty, default(TValue), false);
        }

        // Minimum is used to return the minimum value in the tree
        //~ func (t *Tree) Minimum() (string, interface{}, bool) {
        public (string key, TValue value, bool found) Minimum()
        {
            //!n := t.root
            var n = _root;

            //for {
            //	if n.isLeaf() {
            //		return n.leaf.key, n.leaf.val, true
            //	}
            //	if len(n.edges) > 0 {
            //		n = n.edges[0].node
            //	} else {
            //		break
            //	}
            //}
            while (true)
            {
                if (n.IsLeaf)
                    return (n._leaf._key, n._leaf._value, true);

                if (n._edges.Count > 0)
                    n = n._edges.Values[0]._node;
                else
                    break;
            }

            //return "", nil, false
            return (string.Empty, default(TValue), false);
        }

        // Maximum is used to return the maximum value in the tree
        //~ func (t *Tree) Maximum() (string, interface{}, bool) {
        public (string key, TValue value, bool found) Maximum()
        {
            //~ n := t.root
            var n = _root;

            //for {
            //	if num := len(n.edges); num > 0 {
            //		n = n.edges[num-1].node
            //		continue
            //	}
            //	if n.isLeaf() {
            //		return n.leaf.key, n.leaf.val, true
            //	}
            //	break
            //}
            while (true)
            {
                var num = n._edges.Count;
                if (num > 0)
                {
                    n = n._edges.Values[num - 1]._node;
                    continue;
                }
                if (n.IsLeaf)
                    return (n._leaf._key, n._leaf._value, true);

                break;
            }

            //return "", nil, false
            return (string.Empty, default(TValue), false);
        }

        // Walk is used to walk the tree
        //~ func (t *Tree) Walk(fn WalkFn) {
        public void Walk(Walker<TValue> fn)
        {
            RecursiveWalk(_root, fn);
        }


        // WalkPrefix is used to walk the tree under a prefix
        //~ func (t *Tree) WalkPrefix(prefix string, fn WalkFn) {
        public void WalkPrefix(string prefix, Walker<TValue> fn)
        {
            //~ n := t.root
            //~ search := prefix
            var n = _root;
            var search = prefix;

            //~ for {
            while (true)
            {
                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	recursiveWalk(n, fn)
                //~ 	return
                //~ }
                if (search.Length == 0)
                {
                    RecursiveWalk(n, fn);
                    return;
                }

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	break
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    break;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ 
                //~ } else if strings.HasPrefix(n.prefix, search) {
                //~ 	// Child may be under our search prefix
                //~ 	recursiveWalk(n, fn)
                //~ 	return
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n._prefix))
                {
                    search = search.Substring(n._prefix.Length);
                }
                else if (n._prefix.StartsWith(search))
                {
                    RecursiveWalk(n, fn);
                    return;
                }
                else
                {
                    break;
                }
            }
        }

        // WalkPath is used to walk the tree, but only visiting nodes
        // from the root down to a given leaf. Where WalkPrefix walks
        // all the entries *under* the given prefix, this walks the
        // entries *above* the given prefix.
        //~ func (t *Tree) WalkPath(path string, fn WalkFn) {
        public void WalkPath(string path, Walker<TValue> fn)
        {
            //~ n := t.root
            //~ search := path
            var n = _root;
            var search = path;

            //~ for {
            while (true)
            {
                // Visit the leaf values if any
                //~ if n.leaf != nil && fn(n.leaf.key, n.leaf.val) {
                //~ 	return
                //~ }
                if (n._leaf != null && fn(n._leaf._key, n._leaf._value))
                    return;

                // Check for key exhaution
                //~ if len(search) == 0 {
                //~ 	return
                //~ }
                if (search.Length == 0)
                    return;

                // Look for an edge
                //~ n = n.getEdge(search[0])
                //~ if n == nil {
                //~ 	return
                //~ }
                n = n.GetEdge(search[0]);
                if (n == null)
                    return;

                // Consume the search prefix
                //~ if strings.HasPrefix(search, n.prefix) {
                //~ 	search = search[len(n.prefix):]
                //~ } else {
                //~ 	break
                //~ }
                if (search.StartsWith(n._prefix))
                    search = search.Substring(n._prefix.Length);
                else
                    break;
            }
        }

        // recursiveWalk is used to do a pre-order walk of a node
        // recursively. Returns true if the walk should be aborted
        //~ func recursiveWalk(n *node, fn WalkFn) bool {
        bool RecursiveWalk(Node<TValue> n, Walker<TValue> fn)
        {
            // Visit the leaf values if any
            //~ if n.leaf != nil && fn(n.leaf.key, n.leaf.val) {
            //~ 	return true
            //~ }
            if (n._leaf != null && fn(n._leaf._key, n._leaf._value))
                return true;

            // Recurse on the children
            //~ for _, e := range n.edges {
            //~ 	if recursiveWalk(e.node, fn) {
            //~ 		return true
            //~ 	}
            //~ }
            foreach (var e in n._edges.Values)
            {
                if (RecursiveWalk(e._node, fn))
                    return true;
            }

            //~return false
            return false;
        }

        // ToMap is used to walk the tree and convert it into a map
        //! func (t *Tree) ToMap() map[string]interface{} {
        public IDictionary<string, TValue> ToMap()
        {
            //! out := make(map[string]interface{}, t.size)
            var @out = new Dictionary<string, TValue>(_size);

            //~ t.Walk(func(k string, v interface{}) bool {
            //~ 	out[k] = v
            //~ 	return false
            //~ })
            this.Walk((k, v) =>
            {
                @out[k] = v;
                return false;
            });

            //~ return out
            return @out;
        }

        public void Print(Stream s)
        {
            using (var sw = new StreamWriter(s))
            {
                this._root.Print(sw, "");
            }
        }
    }
}
