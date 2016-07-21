﻿// BudgetFirst 
// ©2016 Thomas Mühlgrabner
//
// This source code is dual-licensed under:
//   * Mozilla Public License 2.0 (MPL 2.0) 
//   * GNU General Public License v3.0 (GPLv3)
//
// ==================== Mozilla Public License 2.0 ===================
// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ================= GNU General Public License v3.0 =================
// This file is part of BudgetFirst.
//
// BudgetFirst is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// BudgetFirst is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Budget First.  If not, see<http://www.gnu.org/licenses/>.
// ===================================================================
namespace BudgetFirst.SharedInterfaces.Messaging
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A Vector Clock that can tell the relative order of events on distributed systems.
    /// </summary>
    [DataContract(Name = "VectorClock", Namespace = "http://budgetfirst.github.io/schemas/2016/07/20/VectorClock")]
    public class VectorClock : IComparable, IReadOnlyDictionary<string, int>
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="VectorClock"/> class.
        /// </summary>
        public VectorClock()
        {
            this.Vector = new Dictionary<string, int>();
            this.Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="VectorClock"/> class.
        /// For internal use only (and tests)
        /// </summary>
        /// <param name="vector">Initial vector</param>
        /// <remarks>Make internal and replace with factory for testing?</remarks>
        public VectorClock(Dictionary<string, int> vector)
        {
            // Always clone vector. The overhead of this is not that bad
            this.Vector = CloneVector(vector);
            this.Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Initialises a new instance of the <see cref="VectorClock"/> class.
        /// Copies an existing vector clock
        /// </summary>
        /// <param name="original">Source vector clock to copy/clone</param>
        private VectorClock(VectorClock original)
        {
            this.Vector = CloneVector(original.Vector);
            this.Timestamp = original.Timestamp;
        }

        /// <summary>
        /// Comparison result for vector clocks
        /// </summary>
        public enum ComparisonResult
        {
            /// <summary>
            /// Both vector clocks are equal
            /// </summary>
            Equal,

            /// <summary>
            /// The vector clock is greater (later)
            /// </summary>
            Greater,

            /// <summary>
            /// The vector clock is smaller (earlier)
            /// </summary>
            Smaller,

            /// <summary>
            /// Both vector clocks are simultaneous
            /// </summary>
            Simultaneous
        }

        /// <summary>
        /// Gets the timestamp of the last increment
        /// </summary>
        [DataMember(Name = "Timestamp")]
        public DateTime Timestamp { get; private set; }

        /// <summary>
        /// Gets an IEnumerable of Keys/Devices in the Vector Clock.
        /// </summary>
        public IEnumerable<string> Keys => this.Vector.Keys;

        /// <summary>
        /// Gets an IEnumerable of integer values in the Vector Clock.
        /// </summary>
        public IEnumerable<int> Values => this.Vector.Values;

        /// <summary>
        /// The number of devices in the Vector Clock.
        /// </summary>
        public int Count => this.Vector.Count;

        /// <summary>
        /// Gets or sets the Vector.
        /// </summary>
        [DataMember(Name = "Vector")]
        private Dictionary<string, int> Vector { get; set; }

        /// <summary>
        /// Gets the value for a particular device.
        /// </summary>
        /// <param name="key">The device</param>
        /// <returns>The value for the device</returns>
        public int this[string key] => this.Vector[key];

        /// <summary>
        /// Create a copy of the current VectorClock and Increment the Vector for the given Device ID by 1 on the new VectorClock
        /// </summary>
        /// <param name="deviceId">The deviceId to increment.</param>
        /// <returns>The new incremented VectorClock</returns>
        public VectorClock Increment(string deviceId)
        {
            var newVector = CloneVector(this.Vector);
            if (newVector.ContainsKey(deviceId))
            {
                newVector[deviceId]++;
            }
            else
            {
                newVector[deviceId] = 1;
            }

            return new VectorClock(newVector);
        }

        /// <summary>
        /// Merges the VectorClock with a second VectorClock by returning a new VectorClock
        /// that has maximum vector value for each device.
        /// </summary>
        /// <param name="clock2">The VectorClock to be merged into the current VectorClock.</param>
        /// <returns>A new VectorClock with the maximum value for each device.</returns>
        public VectorClock Merge(VectorClock clock2)
        {
            var mergedVector = CloneVector(this.Vector);

            // Get maximum from my vectors
            foreach (var deviceId in this.Vector.Keys)
            {
                if (clock2.Vector.ContainsKey(deviceId))
                {
                    mergedVector[deviceId] = Math.Max(this.Vector[deviceId], clock2.Vector[deviceId]);
                }
                else
                {
                    mergedVector[deviceId] = this.Vector[deviceId];
                }
            }

            var newDevices = clock2.Vector.Keys.Where(x => !this.Vector.ContainsKey(x));

            // Get remaining vectors for devices we don't know about yet
            foreach (var deviceId in newDevices)
            {
                mergedVector[deviceId] = clock2.Vector[deviceId];
            }

            return new VectorClock(mergedVector);
        }

        /// <summary>
        /// A function to compare the current VectorClock to another and 
        /// determine which came first, or if the events happened simultaneously.
        /// </summary>
        /// <param name="clock2">The VectorClock to compare.</param>
        /// <returns>A ComparisonResult enum with the result of the comparison.</returns>
        public ComparisonResult CompareVectors(VectorClock clock2)
        {
            /* We check every deviceId that is a key in this vector clock against every deviceId in clock2.
             * If all deviceId values in both clocks are equal they are the same clock(equal).  This result should never happen in BudgetFirst since we always increment the clock for each event.
             * If every deviceId value in this clock is less than the deviceId in clock2 then this VectorClock came before clock2. Some of the deviceIds can be equal but at least one must be less for this case.
             * If every deviceId value in this clock is greater than the deviceId in clock2 then this VectorClock came after clock2.   Some of the deviceIds can be equal but at least one must be greater for this case.
             * If at least one deviceId is greater, and at least one other deviceId is less between the two clocks, the events happened simultaneously.
             */

            var equal = true;
            var greater = true;
            var smaller = true;

            foreach (var deviceId in this.Keys)
            {
                if (clock2.ContainsKey(deviceId))
                {
                    if (this[deviceId] < clock2[deviceId])
                    {
                        // other clock is ahead for this device, so we're most likely earlier or simultaneous
                        equal = false;
                        greater = false;
                    }

                    if (this[deviceId] > clock2[deviceId])
                    {
                        // other clock is behind for this device, so we're most likely later or simultaneous
                        equal = false;
                        smaller = false;
                    }
                }
                else if (this[deviceId] != 0)
                {
                    // other clock doesn't know about this device, so we assume to be later or simultaneous
                    // TODO: does this assumption hold true? What is this based on?
                    equal = false;
                    smaller = false;
                }
            }

            // Check if clock2 has any deviceIds that are not present in this VectorClock
            foreach (var deviceId in clock2.Keys)
            {
                if (!this.ContainsKey(deviceId) && clock2[deviceId] != 0)
                {
                    // We cannot be later because we don't know about those other devices?
                    // So we can only be earlier, or simultaneous
                    // TODO: does this hold true?
                    equal = false;
                    greater = false;
                }
            }

            if (equal)
            {
                // The vectors are the same
                return ComparisonResult.Equal;
            }
            else if (greater && !smaller)
            {
                return ComparisonResult.Greater;
            }
            else if (smaller && !greater)
            {
                return ComparisonResult.Smaller;
            }
            else
            {
                // The events were simultaneous
                return ComparisonResult.Simultaneous;
            }
        }

        /// <summary>
        /// The default comparer for VectorClock.  Determines the order of events 
        /// based on the vector and the timestamp.
        /// </summary>
        /// <param name="obj">The VectorClock to compare to.</param>
        /// <returns>1 if this VectorClock happened after, 
        /// -1 if this VectorClock happened before,
        /// 0 if the order can not be determined(Simultaneous or Equal Vectors and the same timestamp)</returns>
        public int CompareTo(object obj)
        {
            var clock2 = obj as VectorClock;
            var compare = this.CompareVectors(clock2);

            switch (compare)
            {
                case ComparisonResult.Greater:
                    return 1;
                case ComparisonResult.Smaller:
                    return -1;
                default:
                    return DateTime.Compare(this.Timestamp, clock2.Timestamp);
            }
        }

        /// <summary>
        /// Create an exact copy of this current VectorClock.
        /// </summary>
        /// <returns>A copy of the current VectorClock</returns>
        public VectorClock Copy()
        {
            return new VectorClock(this);
        }

        /// <summary>
        /// Returns a boolean value of whether the VectorClock contains a device.
        /// </summary>
        /// <param name="key">The device</param>
        /// <returns>True if the device is present in the VectorClock, otherwise false</returns>
        public bool ContainsKey(string key) => this.Vector.ContainsKey(key);

        /// <summary>
        /// Tries to get the value for a device, and returns a boolean if the get is successful or not.
        /// </summary>
        /// <param name="key">The device</param>
        /// <param name="value">The value for the device</param>
        /// <returns>Whether the get is successful or not</returns>
        public bool TryGetValue(string key, out int value) => this.Vector.TryGetValue(key, out value);

        /// <summary>
        /// Gets an Enumerator of KeyValuePairs representing the VectorClock.
        /// </summary>
        /// <returns>The Enumerator</returns>
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => this.Vector.GetEnumerator();

        /// <summary>
        /// Gets the Enumerator for the VectorClock's Vector.
        /// </summary>
        /// <returns>The Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator() => (this.Vector as IEnumerable).GetEnumerator();

        /// <summary>
        /// Creates a dictionary copy/clone of the vector.
        /// </summary>
        /// <param name="vector">Vector to clone</param>
        /// <returns>Copy/clone of the vector</returns>
        private static Dictionary<string, int> CloneVector(Dictionary<string, int> vector)
        {
            return vector.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
