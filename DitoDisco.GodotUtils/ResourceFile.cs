using System;
using System.Collections.Generic;


namespace DitoDisco.GodotUtils {

    class ResourceFile {

        public enum Token {
            Invalid = 0,

            TagStart,
            TagName,
            TagEnd,
            TagFieldName,
            TagFieldEquals,

            PropertyName,
            PropertyEquals,

            String,
            Number,
            Boolean,
            Callable,
            StartParenthesis,
            EndParenthesis,
        }

        static readonly int DEFAULT = FiniteStateTagger<char, Token>.DEFAULT_STATE_ID;
        const int TAG_START = 1;


        static FiniteStateTagger<char, Token> tagger;

        static ResourceFile() {
            tagger = new FiniteStateTagger<char, Token>(
                new (int, FiniteStateTagger<char, Token>.Transition)[] {
                    (DEFAULT, new(DEFAULT, char.IsWhiteSpace )),
                    (DEFAULT, new(TAG_START, ch => (ch == '[') )),
                }
            );
        }

    }

}
