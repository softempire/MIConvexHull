﻿/*************************************************************************
 *     This file & class is part of the MIConvexHull Library Project. 
 *     Copyright 2006, 2008 Matthew Ira Campbell, PhD.
 *
 *     MIConvexHull is free software: you can redistribute it and/or modify
 *     it under the terms of the GNU General Public License as published by
 *     the Free Software Foundation, either version 3 of the License, or
 *     (at your option) any later version.
 *  
 *     MIConvexHull is distributed in the hope that it will be useful,
 *     but WITHOUT ANY WARRANTY; without even the implied warranty of
 *     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *     GNU General Public License for more details.
 *  
 *     You should have received a copy of the GNU General Public License
 *     along with MIConvexHull.  If not, see <http://www.gnu.org/licenses/>.
 *     
 *     Please find further details and contact information on GraphSynth
 *     at http://miconvexhull.codeplex.com
 *************************************************************************/
namespace MIConvexNameSpace
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// MIConvexHull ("my convex hull", my initials are MIC) is built to be fast for large numbers of
    /// 2D points. Well, it should be fast for small numbers as well with the exception that C# is perhaps
    /// a little slower than C++ for the small amount of array math that'd happen for a small number 
    /// (l.t.20) of vertices.
    /// This reasons it is fast are:
    /// 1. implementing the Akl-Toussaint "octagon" heuristic
    /// 2. using quick dot- and cross-products instead of more expensie sine and cosine functions.
    /// 3. the data structure (an array of sorted-lists of tuples) is calculated and stored to avoid 
    ///    future recalculation.
    /// 4. a single break function to further speed up inter-vectex checking
    /// 5. an ordered list reduces the number of checks for new convex candidates.
    /// </summary>
    public static partial class MIConvexHull
    {
        /// <summary>
        /// Finds the convex hull vertices.
        /// </summary>
        /// <param name="vertices">All of the vertices as a list.</param>
        /// <returns></returns>
        public static List<vertex> Find2D(List<vertex> vertices)
        {
            /* first, the original vertices are copied as they will be modified
             * by this function. */
            var origVertices = new List<vertex>(vertices);
            var origVNum = origVertices.Count;

            #region Step 1 : Define Convex Octogon
            /* The first step is to quickly identify the three to eight vertices based on the
             * Akl-Toussaint heuristic. */
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double maxSum = double.NegativeInfinity;
            double maxDiff = double.NegativeInfinity;
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double minSum = double.PositiveInfinity;
            double minDiff = double.PositiveInfinity;

            /* the array of extreme is comprised of: 0.minX, 1. minSum, 2. minY, 3. maxDiff, 4. MaxX, 5. MaxSum, 6. MaxY, 7. MinDiff. */
            vertex[] extremeVertices = new vertex[8];
            //  int[] extremeVertexIndices = new int[8]; I thought that this might speed things up. That is, to use this to RemoveAt
            // as oppoaws to the Remove in line 91, which I thought might be slow. Turns out I was wrong - plus code is more succinct
            // way.
            for (int i = origVNum - 1; i >= 0; i--)
            {
                var n = origVertices[i];
                if (n.X < minX) { extremeVertices[0] = n; minX = n.X; }
                if ((n.X + n.Y) < minSum) { extremeVertices[1] = n; minSum = n.X + n.Y; }
                if (n.Y < minY) { extremeVertices[2] = n; minY = n.Y; }
                if ((n.X - n.Y) > maxDiff) { extremeVertices[3] = n; maxDiff = n.X - n.Y; }
                if (n.X > maxX) { extremeVertices[4] = n; maxX = n.X; }
                if ((n.X + n.Y) > maxSum) { extremeVertices[5] = n; maxSum = n.X + n.Y; }
                if (n.Y > maxY) { extremeVertices[6] = n; maxY = n.Y; }
                if ((n.X - n.Y) < minDiff) { extremeVertices[7] = n; minDiff = n.X - n.Y; }
            }

            /* convexHullCCW is the result of this function. It is a list of 
             * vertices found in the original vertices and ordered to make a
             * counter-clockwise loop beginning with the leftmost (minimum
             * value of X) vertex. */
            var convexHullCCW = new List<vertex>();
            for (int i = 0; i < 8; i++)
                if (!convexHullCCW.Contains(extremeVertices[i]))
                {
                    convexHullCCW.Add(extremeVertices[i]);
                    origVertices.Remove(extremeVertices[i]);
                }
            #endregion

            /* the following limits are used extensively in for-loop below. In order to reduce the arithmetic calls and
             * steamline the code, these are established. */
            origVNum = origVertices.Count;
            var cvxVNum = convexHullCCW.Count;
            var last = cvxVNum - 1;

            #region Step 2 : Find Signed-Distance to each convex edge
            /* Of the 3 to 8 vertices identified in the convex hull, we now define a matrix called edgeUnitVectors, 
             * which includes the unit vectors of the edges that connect the vertices in a counter-clockwise loop. 
             * The first column corresponds to the X-value,and  the second column to the Y-value. Calculating this 
             * should not take long since there are only 3 to 8 members currently in hull, and it will save time 
             * comparing to all the result vertices. */
            var edgeUnitVectors = new double[cvxVNum, 2];
            double magnitude;
            for (int i = 0; i < last; i++)
            {
                edgeUnitVectors[i, 0] = (convexHullCCW[i + 1].X - convexHullCCW[i].X);
                edgeUnitVectors[i, 1] = (convexHullCCW[i + 1].Y - convexHullCCW[i].Y);
                magnitude = Math.Sqrt(edgeUnitVectors[i, 0] * edgeUnitVectors[i, 0] +
                    edgeUnitVectors[i, 1] * edgeUnitVectors[i, 1]);
                edgeUnitVectors[i, 0] /= magnitude;
                edgeUnitVectors[i, 1] /= magnitude;
            }
            edgeUnitVectors[last, 0] = convexHullCCW[0].X - convexHullCCW[last].X;
            edgeUnitVectors[last, 1] = convexHullCCW[0].Y - convexHullCCW[last].Y;
            magnitude = Math.Sqrt(edgeUnitVectors[last, 0] * edgeUnitVectors[last, 0] +
                edgeUnitVectors[last, 1] * edgeUnitVectors[last, 1]);
            edgeUnitVectors[last, 0] /= magnitude;
            edgeUnitVectors[last, 1] /= magnitude;

            /* Originally, I was storing all the distances from the vertices to the convex hull points
             * in a big 3D matrix. This is not necessary and the storage may be difficult to handle for large
             * sets. However, I have kept these lines of code here because they could be useful in establishing
             * the voronoi sets. */
            //var signedDists = new double[2, origVNum, cvxVNum];

            /* An array of lists of tuples! As we find new candidate convex points, we store them here. The second
             * part of the tuple (Item2 is a double) is the "positionAlong" - this is used to order the nodes that
             * are found for a particular side (More on this in 23 lines). */
            var hullCands = new List<Tuple<vertex, double>>[cvxVNum];
            /* initialize the 3 to 8 Lists s.t. members can be added below. */
            for (int j = 0; j < cvxVNum; j++) hullCands[j] = new List<Tuple<vertex, double>>();

            /* Now a big loop. For each of the original vertices, check them with the 3 to 8 edges to see if they 
             * are inside or out. If they are out, add them to the proper row of the hullCands array. */
            for (int i = 0; i < origVNum; i++)
            {
                for (int j = 0; j < cvxVNum; j++)
                {
                    var bX = origVertices[i].X - convexHullCCW[j].X;
                    var bY = origVertices[i].Y - convexHullCCW[j].Y;
                    //signedDists[0, k, i] = signedDistance(convexVectInfo[i, 0], convexVectInfo[i, 1], bX, bY, convexVectInfo[i, 2]);
                    //signedDists[1, k, i] = positionAlong(convexVectInfo[i, 0], convexVectInfo[i, 1], bX, bY, convexVectInfo[i, 2]);
                    //if (signedDists[0, k, i] <= 0)
                    /* Again, these lines are commented because the signedDists has been removed. This data may be useful in 
                     * other applications though. In the condition below, any signed distance that is negative is outside of the
                     * original polygon. It is only possible for the vertex to be outside one of the 3 to 8 edges, so once we
                     * add it, we break out of the inner loop (gotta save time where we can!). */
                    if (crossProduct(edgeUnitVectors[j, 0], edgeUnitVectors[j, 1], bX, bY) <= 0)
                    {
                        //var newSideCand = Tuple.Create(oldNodes[k], signedDists[1, k, i]);
                        /* In order to improve the efficiency of Step 3, we add the new vertices to a sorted List. Perhaps
                         * this while loop could be replaced with a binary sort inserter to be more efficient. The sort is
                         * done based on the dot product or "positionAlong", this is the signedDistance parallel to the edge 
                         * starting at the vertex (basically = |b|*cos(theta)). All values though will be positive if they
                         * violate the above. */
                        var newSideCand = Tuple.Create(origVertices[i],
                            dotProduct(edgeUnitVectors[j, 0], edgeUnitVectors[j, 1], bX, bY));
                        int k = 0;
                        while ((k < hullCands[j].Count) && (newSideCand.Item2 > hullCands[j][k].Item2)) k++;
                        hullCands[j].Insert(k, newSideCand);
                        break;
                    }
                }
            }
            #endregion

            #region Step 3: now check the remaining hull candidates
            /* Now it's time to go through our array of sorted lists of tuples. We search backwards through
             * the current convex hull points s.t. any additions will not confuse our for-loop indexers. */
            for (int j = cvxVNum; j > 0; j--)
            {
                if (hullCands[j - 1].Count == 1)
                    /* If there is one and only one candidate, it must be in the convex hull. Add it now. */
                    convexHullCCW.Insert(j, hullCands[j - 1][0].Item1);
                else if (hullCands[j - 1].Count > 1)
                {
                    /* If there's more than one than...Well, now comes the tricky part. Here is where the
                     * most time is spent for large sets. this is the O(N*logN) part (the previous steps
                     * were all linear). The above octagon trick was to conquer and divide the candidates. */

                    /* a renaming for compactness and clarity */
                    var hc = hullCands[j - 1];

                    /* put the known starting vertex as the beginning of the list. No need for the "positionAlong"
                     * anymore since the list is now sorted. At any rate, the positionAlong is zero. */
                    hc.Insert(0, Tuple.Create(convexHullCCW[j - 1], 0.0));
                    /* put the ending vertex on the end of the list. Need to check if it wraps back around to 
                     * the first in the loop (hence the simple condition). */
                    if (j == cvxVNum) hc.Add(Tuple.Create(convexHullCCW[0], double.NaN));
                    else hc.Add(Tuple.Create(convexHullCCW[j], double.NaN));

                    /* Now starting from second from end, work backwards looks for places where the angle 
                     * between the vertices in concave (which would produce a negative value of z). */
                    int i = hc.Count - 2;
                    while (i > 0)
                    {
                        var zValue = crossProduct(hc[i].Item1.X - hc[i - 1].Item1.X,
                            hc[i].Item1.Y - hc[i - 1].Item1.Y,
                            hc[i + 1].Item1.X - hc[i].Item1.X,
                            hc[i + 1].Item1.Y - hc[i].Item1.Y);
                        if (zValue < 0)
                        {
                            /* remove any vertices that create concave angles. */
                            hc.RemoveAt(i);
                            /* but don't reduce k since we need to check the previous angle again. Well, 
                             * if you're back to the end you do need to reduce k (hence the line below). */
                            if (i == hc.Count - 1) i--;
                        }
                        /* if the angle is convex, then continue toward the start, k-- */
                        else i--;
                    }
                    /* for each of the remaining vertices in hullCands[i-1], add them to the convexHullCCW. 
                     * Here we insert them backwards (k counts down) to simplify the insert operation (k.e.
                     * since all are inserted @ i, the previous inserts are pushed up to i+1, i+2, etc. */
                    for (i = hc.Count - 2; i > 0; i--)
                        convexHullCCW.Insert(j, hc[i].Item1);
                }
            }
            #endregion

            /* finally return the hull points. */
            return convexHullCCW;
        }


        /// <summary>
        /// An overload that takes the vertices as an nX2 matrix, where the first column
        /// is the x values of the matrix and the second column is the y values. It returns,
        /// a similar matrix comprised only of the convex hull ordered in a counter-clock-wise loop.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <returns></returns>
        public static double[,] Find2D(double[,] vertices)
        {
            var numRows = vertices.GetLength(0);
            var vList = new List<vertex>(numRows);
            for (int i = 0; i < numRows; i++)
                vList.Add(new vertex(vertices[i, 0], vertices[i, 1]));

            List<vertex> convexHull = Find2D(vList);
            numRows = convexHull.Count;
            double[,] result = new double[numRows, 2];
            for (int i = 0; i < numRows; i++)
            {
                result[i, 0] = convexHull[i].X;
                result[i, 1] = convexHull[i].Y;
            }
            return result;
        }

    }
}