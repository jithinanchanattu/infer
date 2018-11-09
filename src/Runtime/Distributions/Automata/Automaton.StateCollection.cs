// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.ML.Probabilistic.Distributions.Automata
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.ML.Probabilistic.Distributions;
    using Microsoft.ML.Probabilistic.Math;

    public abstract partial class Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis>
        where TSequence : class, IEnumerable<TElement>
        where TElementDistribution : IDistribution<TElement>, SettableToProduct<TElementDistribution>, SettableToWeightedSumExact<TElementDistribution>, CanGetLogAverageOf<TElementDistribution>, SettableToPartialUniform<TElementDistribution>, new()
        where TSequenceManipulator : ISequenceManipulator<TSequence, TElement>, new()
        where TThis : Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis>, new()
    {
        /// <summary>
        /// Represents a collection of automaton states for use in public APIs
        /// </summary>
        /// <remarks>
        /// Is a thin wrapper around Automaton.stateData. Wraps each <see cref="StateData"/> into <see cref="State"/> on demand.
        /// </remarks>
        public struct StateCollection : IReadOnlyList<State>
        {
            /// <summary>
            /// Owner automaton of all states in collection.
            /// </summary>
            private readonly Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis> owner;

            /// <summary>
            /// TODO
            /// </summary>
            internal List<StateData> states;

            /// <summary>
            /// Initializes instance of <see cref="StateCollection"/>.
            /// </summary>
            internal StateCollection(
                Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis> owner,
                List<StateData> initStates = null)
            {
                this.owner = owner;
                this.states = initStates ?? new List<StateData>();
            }

            /// <summary>
            /// Adds a new state to the automaton.
            /// </summary>
            /// <returns>The added state.</returns>
            /// <remarks>Indices of the added states are guaranteed to be increasing consecutive.</remarks>
            internal State Add()
            {
                if (this.states.Count >= maxStateCount)
                {
                    throw new AutomatonTooLargeException(MaxStateCount);
                }

                var index = this.states.Count;
                var stateData = new StateData();
                this.states.Add(stateData);

                return new State(this.owner, index, stateData);
            }

            /// <summary>
            /// Removes the state with a given index from the automaton.
            /// </summary>
            /// <param name="index">The index of the state to remove.</param>
            /// <remarks>
            /// The automaton representation we currently use does not allow for fast state removal.
            /// Ideally we should get rid of this function completely.
            /// </remarks>
            internal void Remove(int index)
            {
                Debug.Assert(index >= 0 && index < this.states.Count, "An invalid state index provided.");
                Debug.Assert(index != this.owner.startStateIndex, "Cannot remove the start state.");

                this.states.RemoveAt(index);
                var stateCount = this.states.Count;
                for (var i = 0; i < stateCount; i++)
                {
                    StateData state = this.states[i];
                    for (var j = state.TransitionCount - 1; j >= 0; j--)
                    {
                        var transition = state.GetTransition(j);
                        if (transition.DestinationStateIndex == index)
                        {
                            state.RemoveTransition(j);
                        }

                        if (transition.DestinationStateIndex > index)
                        {
                            transition.DestinationStateIndex = transition.DestinationStateIndex - 1;
                        }

                        state.SetTransition(j, transition);
                    }
                }
            }

            /// <summary>
            /// Adds the states in a given collection to the automaton together with their transitions,
            /// but without attaching any of them to any of the existing states.
            /// </summary>
            /// <param name="statesToAdd">The states to add.</param>
            /// <param name="group">The group for the transitions of the states being added.</param>
            internal void Append(IReadOnlyList<State> statesToAdd, int group = 0)
            {
                Debug.Assert(statesToAdd != null, "A valid state collection must be provided.");

                var startIndex = this.states.Count;
                this.states.Capacity = this.states.Count + statesToAdd.Count;

                // Add states
                for (var i = 0; i < statesToAdd.Count; ++i)
                {
                    var newState = this.Add();
                    newState.SetEndWeight(statesToAdd[i].EndWeight);

                    Debug.Assert(newState.Index == i + startIndex, "State indices must always be consequent.");
                }

                // Add transitions
                for (var i = 0; i < statesToAdd.Count; ++i)
                {
                    var stateToAdd = statesToAdd[i];
                    for (var transitionIndex = 0; transitionIndex < stateToAdd.TransitionCount; transitionIndex++)
                    {
                        var transitionToAdd = stateToAdd.GetTransition(transitionIndex);
                        Debug.Assert(transitionToAdd.DestinationStateIndex < statesToAdd.Count, "Self-inconsistent collection of states provided.");
                        this[i + startIndex].AddTransition(
                            transitionToAdd.ElementDistribution,
                            transitionToAdd.Weight,
                            this[transitionToAdd.DestinationStateIndex + startIndex],
                            group != 0 ? group : transitionToAdd.Group);
                    }
                }
            }

            internal void SetTo(IReadOnlyList<State> that)
            {
                this.states.Clear();
                this.Append(that);
            }

            #region IReadOnlyList<State> methods

            /// <summary>
            /// Gets state by its index.
            /// </summary>
            public State this[int index] => new State(this.owner, index, this.states[index]);

            /// <summary>
            /// Gets number of states in collection.
            /// </summary>
            public int Count => this.states.Count;

            /// <summary>
            /// Returns enumerator over all states in collection.
            /// </summary>
            public IEnumerator<State> GetEnumerator()
            {
                var owner = this.owner;
                return this.states.Select((data, index) => new State(owner, index, data)).GetEnumerator();
            }

            /// <summary>
            /// Returns enumerator over all states in collection.
            /// </summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            #endregion
        }
    }
}
