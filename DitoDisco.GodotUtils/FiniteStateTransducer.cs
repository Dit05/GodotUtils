using System;
using System.Collections.Generic;


namespace DitoDisco.GodotUtils {

    // FIXME not all of the text should be tagged
    internal class FiniteStateTagger<TIn, TTag> where TIn : notnull {

        public static readonly int DEFAULT_STATE_ID = 0;


        public class InputRejectedException : Exception {

            public int State { get; private init; }
            public TIn? Input { get; private init; }
            bool inputWasEos = false;

            public override string Message =>
                inputWasEos
                ? $"State {State} has no transition for (end of sequence). A transition matches (end of sequence) when its condition is null."
                : $"State {State} has no transition for {Input?.ToString() ?? "(null)"}.";


            public static InputRejectedException EndOfSequence(int state) {
                return new InputRejectedException(state);
            }
            private InputRejectedException(int state) {
                this.State = state;
                this.Input = default;
                inputWasEos = true;
            }

            public InputRejectedException(int state, TIn input) {
                this.State = state;
                this.Input = input;
            }

        }

        public struct Transition {
            public int nextState;
            /// <summary>No condition matches the end-of-sequence.</summary>
            /// <remarks>I wish C# had tagged unions.</remarks>
            public Predicate<TIn>? condition;

            bool _isTagging;
            [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(appliedTag))]
            public bool IsTagging => _isTagging;

            public TTag? appliedTag;


            public Transition(int nextState, Predicate<TIn>? condition = null) {
                this.nextState = nextState;
                this.condition = condition;
                this._isTagging = false;
                this.appliedTag = default;
            }

            public Transition(int nextState, TTag appliedTag, Predicate<TIn>? condition = null) {
                this.nextState = nextState;
                this.condition = condition;
                this._isTagging = true;
                this.appliedTag = appliedTag;
            }
        }

        class State {
            public readonly int sequentialId;
            public readonly List<Transition> transitions = new List<Transition>();

            public State(int sequentialId) {
                this.sequentialId = sequentialId;
            }
        }


        // TODO can be replaced with an array if the role of id and sequentialId are swapped
        private readonly Dictionary<int, State> states = new Dictionary<int, State>();
        public int StateCount => states.Count;



        public FiniteStateTagger(IEnumerable<(int stateId, Transition transition)> stateTransitions) {
            int nextSequentialId = 0;

            State GetOrCreateState(int id) {
                State? state;
                if(!states.TryGetValue(id, out state)) {
                    state = new State(nextSequentialId++);
                    states.Add(id, state);
                }
                return state;
            }

            foreach((int stateId, Transition transition) kvp in stateTransitions) {
                GetOrCreateState(kvp.stateId).transitions.Add(kvp.transition);
                _ = GetOrCreateState(kvp.transition.nextState);
            }

            if(!states.ContainsKey(DEFAULT_STATE_ID)) throw new ArgumentException($"There must be a state with id {nameof(FiniteStateTagger<TIn, TTag>)}.{nameof(DEFAULT_STATE_ID)}");
        }



        public IEnumerable<(TTag tag, int start, int length)> TagSequence(IEnumerable<TIn> inputSequence, int[]? scratchHotTransitionLookup = null) {
            int[] hotTransitionLookup;
            if(scratchHotTransitionLookup is not null) {
                if(scratchHotTransitionLookup.Length < StateCount) throw new ArgumentException($"{nameof(scratchHotTransitionLookup)}'s length must be at least {nameof(StateCount)}.", nameof(scratchHotTransitionLookup));
                hotTransitionLookup = scratchHotTransitionLookup;
                Array.Fill(hotTransitionLookup, 0);
            } else {
                hotTransitionLookup = new int[StateCount];
            }


            int tagStart = 0;
            int index = 0;

            int stateId = DEFAULT_STATE_ID;
            State state = states[stateId]; // Guaranteed to exist in the constructor.
            foreach(TIn input in inputSequence) {
                int hotIndex = hotTransitionLookup[state.sequentialId];
                int matchedIndex = -1;

                for(int i = hotIndex; i < state.transitions.Count; i++) {
                    if(state.transitions[i].condition?.Invoke(input) ?? false) {
                        matchedIndex = i;
                        break;
                    }
                }

                if(matchedIndex < 0) {
                    for(int i = 0; i < hotIndex; i++) {
                        if(state.transitions[i].condition?.Invoke(input) ?? false) {
                            matchedIndex = i;
                            break;
                        }
                    }
                }


                if(matchedIndex < 0) {
                    throw new InputRejectedException(stateId, input);
                } else {
                    hotTransitionLookup[state.sequentialId] = matchedIndex;
                    Transition transition = state.transitions[matchedIndex];

                    stateId = transition.nextState;
                    state = states[stateId];

                    if(transition.IsTagging) {
                        yield return (transition.appliedTag, tagStart, index - tagStart);
                        tagStart = index;
                    }
                }

                index++;
            }


            // Try to match the end of sequence
            for(int i = 0; i < state.transitions.Count; i++) {
                Transition transition = state.transitions[i];
                if(transition.condition is null) {
                    if(transition.IsTagging) yield return (transition.appliedTag, tagStart, index - tagStart);
                    yield break;
                }
            }

            throw InputRejectedException.EndOfSequence(stateId);
        }

    }

}
