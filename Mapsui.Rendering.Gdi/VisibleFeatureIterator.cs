// Copyright 2005, 2006 - Morten Nielsen (www.iter.dk)
// Copyright 2010 - Paul den Dulk (Geodan) - Adapted SharpMap for Mapsui
//
// This file is part of Mapsui.
// Mapsui is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// Mapsui is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with Mapsui; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using System.Threading;

namespace Mapsui.Rendering.Gdi
{
    internal class VisibleFeatureIterator
    {
        public static void IterateLayers(Graphics graphics, IViewport viewport, IEnumerable<ILayer> layers,
            Action<IViewport, IStyle, IFeature, StyleContext> callback)
        {
            using (var styleContext = new StyleContext())
            {
                foreach (var layer in layers)
                {
                    IterateLayer(graphics, viewport, layer, styleContext, callback);
                }
            }
        }

        public static void IterateLayer(Graphics graphics, IViewport viewport, ILayer layer,
            Action<IViewport, IStyle, IFeature, StyleContext> callback)
        {
            using (var styleContext = new StyleContext())
            {
                IterateLayer(graphics, viewport, layer, styleContext, callback);
            }
        }

        private static void IterateLayer(Graphics graphics, IViewport viewport, ILayer layer, StyleContext context,
            Action<IViewport, IStyle, IFeature, StyleContext> callback)
        {
            if (layer.Enabled == false) return;
            if (layer.MinVisible > viewport.RenderResolution) return;
            if (layer.MaxVisible < viewport.RenderResolution) return;

            if (layer is LabelLayer)
            {
                LabellayerRenderer.Render(graphics, viewport, layer as LabelLayer, context);
            }
            else
            {
                IterateVectorLayer(viewport, layer, context, callback);
            }
        }

        private static void IterateVectorLayer(IViewport viewport, ILayer layer, StyleContext context,
            Action<IViewport, IStyle, IFeature, StyleContext> callback)
        {
            var features = layer.GetFeaturesInView(viewport.Extent, viewport.RenderResolution)
                .Select((f, i) =>
                {
                    ReleaseLoad(i);
                    return f;
                })
                .Where(f =>
                {
                    if (f.Geometry is Mapsui.Geometries.Point) return true;
                    var boundingBox = f.Geometry.GetBoundingBox();
                    return boundingBox.Height / viewport.Resolution > 0.1 ||
                        boundingBox.Width / viewport.Resolution > 0.1;
                })
            .ToList();

            var layerStyles = layer.Style is StyleCollection ? (layer.Style as StyleCollection).ToArray() : new[] { layer.Style };
            foreach (var layerStyle in layerStyles)
            {
                var style = layerStyle; // This is the default that could be overridden by an IThemeStyle

                if ((style == null) || (style.Enabled == false) || (style.MinVisible > viewport.RenderResolution) || (style.MaxVisible < viewport.RenderResolution)) continue;

                for (int i = 0; i < features.Count; i++)
                {
                    var feature = features[i];

                    if (layerStyle is IThemeStyle) style = (layerStyle as IThemeStyle).GetStyle(feature);

                    callback(viewport, style, feature, context);

                    ReleaseLoad(i);
                }
            }

            for (int i = 0; i < features.Count; i++)
            {
                var feature = features[i];

                if (feature.Styles != null)
                {
                    foreach (var featureStyle in feature.Styles)
                    {
                        if (feature.Styles != null && featureStyle.Enabled)
                        {
                            callback(viewport, featureStyle, feature, context);
                        }
                    }
                }

                ReleaseLoad(i);
            }

            ReleaseLoad(0);
        }

        private static void ReleaseLoad(int i=0)
        {
            const int ReleaseCount = 100;
            if ((i % ReleaseCount) == 0)
            {
                Thread.Sleep(0);
            }
        }
    }
}