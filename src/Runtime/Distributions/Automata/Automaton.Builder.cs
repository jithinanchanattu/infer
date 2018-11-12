// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.ML.Probabilistic.Distributions.Automata
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using Microsoft.ML.Probabilistic.Collections;
    using Microsoft.ML.Probabilistic.Distributions;
    using Microsoft.ML.Probabilistic.Math;
    using Microsoft.ML.Probabilistic.Serialization;
    using Microsoft.ML.Probabilistic.Utilities;

    public abstract partial class Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis>
        where TSequence : class, IEnumerable<TElement>
        where TElementDistribution : IDistribution<TElement>, SettableToProduct<TElementDistribution>,
        SettableToWeightedSumExact<TElementDistribution>, CanGetLogAverageOf<TElementDistribution>,
        SettableToPartialUniform<TElementDistribution>, new()
        where TSequenceManipulator : ISequenceManipulator<TSequence, TElement>, new()
        where TThis : Automaton<TSequence, TElement, TElementDistribution, TSequenceManipulator, TThis>, new()
    {
        /// <summary>
        /// Represents a reference to a state of automaton for exposure in public API.
        /// </summary>
        /// <remarks>
        /// Acts as a "fat reference" to state in automaton. In addition to reference to actual StateData it carries
        /// 2 additional properties for convinience: <see cref="Owner"/> automaton and <see cref="Index"/> of the state.
        /// We don't store them in <see cref="StateData"/> to save some memoty. C# compiler and .NET jitter are good
        /// at optimizing wrapping where it is not needed.
        /// </remarks>
        public class Builder
        {
            private readonly List<StateData> states;
            private readonly List<LinkedTransition> transitions;

            public int StartStateIndex { get; }

            public Builder()
            {
                this.states = new List<StateData>();
                this.transitions = new List<LinkedTransition>();
            }

            public static Builder Zero()
            {
                var builder = new Builder();
                builder.AddState();
                return builder;
            }

            public static Builder ConstantOn(double weight, TSequence sequence)
            {
                throw new NotImplementedException();
            }

            public StateBuilder AddState()
            {
                int index = this.states.Count;
                this.states.Add(new StateData {firstTransition = -1});
                return new StateBuilder(this, index);
            }

            public void InsertStates(StateCollection states)
            {
                throw new NotImplementedException();
            }

            public void Append(TThis automaton, bool avoidEpsilonTransitions = true)
            {
                throw new NotImplementedException();
            }

            public int StatesCount => states.Count;

            public StateBuilder this[int index] => new StateBuilder(this, this.StartStateIndex);

            public StateBuilder Start => this[this.StartStateIndex];

            internal StateCollection GetStorage()
            {
                throw new NotImplementedException();
            }

            public TThis GetAutomaton()
            {
                throw new NotImplementedException();
            }

            public struct StateBuilder
            {
                private Builder builder;

                public int Index { get; }

                internal StateBuilder(Builder builder, int index)
                {
                    this.builder = builder;
                    this.Index = index;
                }

                public void SetEndWeight(Weight weight)
                {
                    this.builder.states[this.Index].EndWeight = weight;
                }

                public StateBuilder AddTransition(Transition transition)
                {
                    var transitionIndex = this.builder.transitions.Count;
                    this.builder.transitions.Add(
                        new LinkedTransition
                        {
                            transition = transition,
                            next = this.builder.states[this.Index].firstTransition,
                        });
                    this.builder.states[this.Index].firstTransition = transitionIndex;
                    return new StateBuilder(this.builder, transition.DestinationStateIndex);
                }

                /// <summary>
                /// Adds a transition to the current state.
                /// </summary>
                /// <param name="elementDistribution">
                /// The element distribution associated with the transition.
                /// If the value of this parameter is <see langword="null"/>, an epsilon transition will be created.
                /// </param>
                /// <param name="weight">The transition weight.</param>
                /// <param name="destinationState">
                /// The destination state of the added transition.
                /// If the value of this parameter is <see langword="null"/>, a new state will be created.</param>
                /// <param name="group">The group of the added transition.</param>
                /// <returns>The destination state of the added transition.</returns>
                public StateBuilder AddTransition(
                    Option<TElementDistribution> elementDistribution,
                    Weight weight,
                    int? destinationStateIndex = null,
                    int group = 0)
                {
                    if (destinationStateIndex == null)
                    {
                        destinationStateIndex = this.builder.AddState().Index;
                    }

                    return this.AddTransition(
                        new Transition(elementDistribution, weight, destinationStateIndex.Value, group));
                }


                /// <summary>
                /// Adds a transition labeled with a given element to the current state.
                /// </summary>
                /// <param name="element">The element.</param>
                /// <param name="weight">The transition weight.</param>
                /// <param name="destinationState">
                /// The destination state of the added transition.
                /// If the value of this parameter is <see langword="null"/>, a new state will be created.</param>
                /// <param name="group">The group of the added transition.</param>
                /// <returns>The destination state of the added transition.</returns>
                public StateBuilder AddTransition(
                    TElement element,
                    Weight weight,
                    int? destinationStateIndex = null,
                    int group = 0)
                {
                    return this.AddTransition(
                        new TElementDistribution {Point = element}, weight, destinationStateIndex, group);
                }

                /// <summary>
                /// Adds an epsilon transition to the current state.
                /// </summary>
                /// <param name="weight">The transition weight.</param>
                /// <param name="destinationState">
                /// The destination state of the added transition.
                /// If the value of this parameter is <see langword="null"/>, a new state will be created.</param>
                /// <param name="group">The group of the added transition.</param>
                /// <returns>The destination state of the added transition.</returns>
                public StateBuilder AddEpsilonTransition(
                    Weight weight, int? destinationStateIndex = null, int group = 0)
                {
                    return this.AddTransition(Option.None, weight, destinationStateIndex, group);
                }

                /// <summary>
                /// Adds a self-transition labeled with a given element to the current state.
                /// </summary>
                /// <param name="element">The element.</param>
                /// <param name="weight">The transition weight.</param>
                /// <param name="group">The group of the added transition.</param>
                /// <returns>The current state.</returns>
                public StateBuilder AddSelfTransition(TElement element, Weight weight, int group = 0)
                {
                    return this.AddTransition(element, weight, this.Index, group);
                }

                /// <summary>
                /// Adds a self-transition to the current state.
                /// </summary>
                /// <param name="elementDistribution">
                /// The element distribution associated with the transition.
                /// If the value of this parameter is <see langword="null"/>, an epsilon transition will be created.
                /// </param>
                /// <param name="weight">The transition weight.</param>
                /// <param name="group">The group of the added transition.</param>
                /// <returns>The current state.</returns>
                public StateBuilder AddSelfTransition(
                    Option<TElementDistribution> elementDistribution, Weight weight, byte group = 0)
                {
                    return this.AddTransition(elementDistribution, weight, this.Index, group);
                }


                /// <summary>
                /// Adds a series of transitions labeled with the elements of a given sequence to the current state,
                /// as well as the intermediate states. All the added transitions have unit weight.
                /// </summary>
                /// <param name="sequence">The sequence.</param>
                /// <param name="destinationState">
                /// The last state in the transition series.
                /// If the value of this parameter is <see langword="null"/>, a new state will be created.
                /// </param>
                /// <param name="group">The group of the added transitions.</param>
                /// <returns>The last state in the added transition series.</returns>
                public StateBuilder AddTransitionsForSequence(
                    TSequence sequence,
                    int? destinationStateIndex = null,
                    int group = 0)
                {
                    var currentState = this;
                    using (var enumerator = sequence.GetEnumerator())
                    {
                        var moveNext = enumerator.MoveNext();
                        while (moveNext)
                        {
                            var element = enumerator.Current;
                            moveNext = enumerator.MoveNext();
                            currentState = currentState.AddTransition(
                                element, Weight.One, moveNext ? null : destinationStateIndex, group);
                        }
                    }

                    return currentState;
                }
            }

            private struct LinkedTransition
            {
                public Transition transition;
                public int next;
            }
        }
    }
}