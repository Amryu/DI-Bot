using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DIBot.Utility
{
    public static class HtmlNodeExtension
    {
        public static HtmlNode FirstPreviousSibling(this HtmlNode node, Func<HtmlNode, bool> func)
        {
            var prevSibling = node;

            while (true)
            {
                prevSibling = prevSibling.PreviousSibling;

                if (prevSibling == null) return null;

                if (func.Invoke(prevSibling)) return prevSibling;
            }
        }

        public static HtmlNode FirstChild(this HtmlNode node, Func<HtmlNode, bool> func)
        {
            var child = node.FirstChild;

            while (true)
            {
                if (child == null) return null;

                if (func.Invoke(child)) return child;

                child = child.NextSibling;
            }
        }

        public static HtmlNode FirstParent(this HtmlNode node, Func<HtmlNode, bool> func)
        {
            var parent = node.ParentNode;

            while (true)
            {
                if (parent == null) return null;

                if (func.Invoke(parent)) return parent;

                parent = parent.ParentNode;
            }
        }
    }
}
