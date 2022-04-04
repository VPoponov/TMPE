namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Iterators;
    using static Shortcuts;

    public static class NetSegmentExtensions {
        private static NetSegment[] _segBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

        public static ref NetSegment ToSegment(this ushort segmentId) => ref _segBuffer[segmentId];

        public static ushort GetNodeId(this ref NetSegment segment, bool startNode) =>
            startNode ? segment.m_startNode : segment.m_endNode;

        public static ushort GetHeadNode(this ref NetSegment netSegment) {
            // tail node>-------->head node
            bool invert = netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT;
            if (invert) {
                return netSegment.m_startNode;
            } else {
                return netSegment.m_endNode;
            }
        }

        public static ushort GetTailNode(this ref NetSegment netSegment) {
            bool invert = netSegment.m_flags.IsFlagSet(NetSegment.Flags.Invert) ^ LHT;
            if (!invert) {
                return netSegment.m_startNode;
            } else {
                return netSegment.m_endNode;
            }//endif
        }

        /// <summary>
        /// Check if the <paramref name="nodeId"/> belongs to the
        /// <paramref name="netSegment"/>, and if so determines
        /// whether it is the start or end node.
        /// </summary>
        /// <param name="netSegment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to examine.</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><term><c>true</c></term> <description>start node</description></item>
        /// <item><term><c>false</c></term> <description>end node</description></item>
        /// <item><term><c>null</c></term> <description>not related to segment</description></item>
        /// </list>
        /// </returns>
        /// <example>
        /// Get and process node relationship:
        /// <code>
        /// bool? relation = segmentId.ToSegment().GetRelationToNode(nodeId);
        ///
        /// if (!relation.HasValue) {
        ///     // no relation
        /// } else if (relation.Value) {
        ///     // start node
        /// } else {
        ///     // end node
        /// }
        /// </code>
        /// </example>
        public static bool? GetRelationToNode(this ref NetSegment netSegment, ushort nodeId) {
            if (netSegment.m_startNode == nodeId) {
                return true;
            } else if (netSegment.m_endNode == nodeId) {
                return false;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Determine if specified <paramref name="nodeId"/> is the start node for
        /// the <paramref name="netSegment"/>.
        /// </summary>
        /// <param name="netSegment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to examine.</param>
        /// <returns>
        /// <para>Returns <c>true</c> if start node, otherwise <c>false</c>.</para>
        /// <para>A <c>false</c> return value does not guarantee the node is the
        /// segment end node; the node might not belong to the segment.
        /// If you need to ensure the node is related to the segment, use
        /// <see cref="GetRelationToNode(ref NetSegment, ushort)"/> instead.</para>
        /// </returns>
        public static bool IsStartNode(this ref NetSegment netSegment, ushort nodeId) =>
            netSegment.m_startNode == nodeId;

        /// <summary>
        /// Checks if the netSegment is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="netSegment">netSegment</param>
        /// <returns>True if the netSegment is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetSegment netSegment) =>
            netSegment.m_flags.CheckFlags(
                required: NetSegment.Flags.Created,
                forbidden: NetSegment.Flags.Collapsed | NetSegment.Flags.Deleted);

        public static NetInfo.Lane GetLaneInfo(this ref NetSegment netSegment, int laneIndex) =>
            netSegment.Info?.m_lanes?[laneIndex];

        public static GetSegmentLaneIdsEnumerable GetSegmentLaneIdsAndLaneIndexes(this ref NetSegment netSegment) {
            NetInfo netInfo = netSegment.Info;
            uint initialLaneId = netSegment.m_lanes;
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            if (netInfo == null) {
                return new GetSegmentLaneIdsEnumerable(0, 0, laneBuffer);
            }

            return new GetSegmentLaneIdsEnumerable(initialLaneId, netInfo.m_lanes.Length, laneBuffer);
        }

        /// <summary>
        /// Iterates the lanes in the specified <paramref name="netSegment"/> until it finds one which matches
        /// both the specified <paramref name="laneType"/> and <paramref name="vehicleType"/> masks.
        /// </summary>
        /// <param name="netSegment">The <see cref="NetSegment"/> to inspect.</param>
        /// <param name="laneType">The required <see cref="NetInfo.LaneType"/> flags (at least one must match).</param>
        /// <param name="vehicleType">The required <see cref="VehicleInfo.VehicleType"/> flags (at least one must match).</param>
        /// <returns>Returns <c>true</c> if a lane matches, otherwise <c>false</c> if none of the lanes match.</returns>
        public static bool AnyApplicableLane(
            this ref NetSegment netSegment,
            NetInfo.LaneType laneType,
            VehicleInfo.VehicleType vehicleType) {

            AssertNotNone(laneType, nameof(laneType));
            AssertNotNone(vehicleType, nameof(vehicleType));

            NetManager netManager = Singleton<NetManager>.instance;

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            byte laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneInfo.m_vehicleType & vehicleType) != 0 &&
                    (laneInfo.m_laneType & laneType) != 0) {

                    return true;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            return false;
        }

        /// <summary>
        /// Count the number of lanes matching specified criteria at a segment end.
        ///
        /// Faster than doing <c>GetSortedLanes().Count</c>.
        /// </summary>
        /// <param name="segment">The segment to inspect.</param>
        /// <param name="nodeId">The id of the node to inspect.</param>
        /// <param name="laneTypeFilter">Filter to specified lane types.</param>
        /// <param name="vehicleTypeFilter">Filter to specified vehicle types.</param>
        /// <param name="incoming">
        /// If <c>true</c>, count lanes entering the segment via the node.
        /// if <c>false</c>, count lanes leaving the segment via the node.
        /// </param>
        /// <returns>Returns number of lanes matching specified criteria.</returns>
        /// <remarks>
        /// See also: <c>CountLanes()</c> methods on the <see cref="NetSegment"/> struct.
        /// </remarks>
        public static int CountLanes(
            this ref NetSegment segment,
            ushort nodeId,
            NetInfo.LaneType laneTypeFilter,
            VehicleInfo.VehicleType vehicleTypeFilter,
            bool incoming = false) {

            int count = 0;

            NetInfo segmentInfo = segment.Info;

            if (segmentInfo == null || segmentInfo.m_lanes == null)
                return count;

            bool startNode = segment.IsStartnode(nodeId) ^ incoming;
            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            NetInfo.Direction filterDir = startNode
                ? NetInfo.Direction.Backward
                : NetInfo.Direction.Forward;

            if (inverted)
                filterDir = NetInfo.InvertDirection(filterDir);

            uint curLaneId = segment.m_lanes;
            byte laneIndex = 0;

            NetManager netManager = Singleton<NetManager>.instance;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneInfo.m_finalDirection == filterDir) &&
                    (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None &&
                    (laneInfo.m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None) {

                    count++;
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            return count;
        }

        /// <summary>
        /// Assembles a geometrically sorted list of lanes for the given segment.
        /// If the <paramref name="startNode"/> parameter is set only lanes supporting traffic to flow towards the given node are added to the list, otherwise all matched lanes are added.
        /// </summary>
        /// <param name="netSegment">segment data</param>
        /// <param name="startNode">reference node (optional)</param>
        /// <param name="laneTypeFilter">lane type filter, lanes must match this filter mask</param>
        /// <param name="vehicleTypeFilter">vehicle type filter, lanes must match this filter mask</param>
        /// <param name="reverse">if true, lanes are ordered from right to left (relative to the
        ///     segment's start node / the given node), otherwise from left to right</param>
        /// <param name="sort">if false, no sorting takes place
        ///     regardless of <paramref name="reverse"/></param>
        /// <returns>sorted list of lanes for the given segment</returns>
        public static IList<LanePos> GetSortedLanes(
            this ref NetSegment netSegment,
            bool? startNode,
            NetInfo.LaneType? laneTypeFilter = null,
            VehicleInfo.VehicleType? vehicleTypeFilter = null,
            bool reverse = false,
            bool sort = true) {
            // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
            var laneList = new List<LanePos>();

            NetInfo segmentInfo = netSegment.Info;

            if (segmentInfo == null || segmentInfo.m_lanes == null)
                return laneList;

            bool inverted = (netSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            NetInfo.Direction? filterDir = null;
            NetInfo.Direction sortDir = NetInfo.Direction.Forward;

            if (startNode != null) {
                filterDir = (bool)startNode
                                ? NetInfo.Direction.Backward
                                : NetInfo.Direction.Forward;
                filterDir = inverted
                                ? NetInfo.InvertDirection((NetInfo.Direction)filterDir)
                                : filterDir;
                sortDir = NetInfo.InvertDirection((NetInfo.Direction)filterDir);
            } else if (inverted) {
                sortDir = NetInfo.Direction.Backward;
            }

            if (reverse) {
                sortDir = NetInfo.InvertDirection(sortDir);
            }

            uint curLaneId = netSegment.m_lanes;
            byte laneIndex = 0;

            NetManager netManager = Singleton<NetManager>.instance;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                if ((laneTypeFilter == null ||
                     (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None) &&
                    (vehicleTypeFilter == null || (laneInfo.m_vehicleType & vehicleTypeFilter) !=
                     VehicleInfo.VehicleType.None) &&
                    (filterDir == null ||
                     laneInfo.m_finalDirection == filterDir)) {
                    laneList.Add(
                        new LanePos(
                            curLaneId,
                            laneIndex,
                            laneInfo.m_position,
                            laneInfo.m_vehicleType,
                            laneInfo.m_laneType));
                }

                curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++laneIndex;
            }

            if (sort) {
                int CompareLanePositionsFun(LanePos x, LanePos y) {
                    bool fwd = sortDir == NetInfo.Direction.Forward;
                    if (Math.Abs(x.position - y.position) < 1e-12) {
                        if (x.position > 0) {
                            // mirror type-bound lanes (e.g. for coherent disply of lane-wise speed limits)
                            fwd = !fwd;
                        }

                        if (x.laneType == y.laneType) {
                            if (x.vehicleType == y.vehicleType) {
                                return 0;
                            }

                            if ((x.vehicleType < y.vehicleType) == fwd) {
                                return -1;
                            }

                            return 1;
                        }

                        if ((x.laneType < y.laneType) == fwd) {
                            return -1;
                        }

                        return 1;
                    }

                    if (x.position < y.position == fwd) {
                        return -1;
                    }

                    return 1;
                }

                laneList.Sort(CompareLanePositionsFun);
            }
            return laneList;
        }
    }
}
