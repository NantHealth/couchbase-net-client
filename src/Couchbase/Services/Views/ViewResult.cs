using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTracing;

namespace Couchbase.Services.Views
{
    /// <summary>
    /// Represents a streaming View response for reading each row as it becomes available over the network.
    /// Note that unless there is no underlying collection representing the response, instead the rows are extracted
    /// from the stream one at a time. If the Enumeration is evaluated, eg calling ToList(), then the entire response
    /// will be read. Once a row has been read from the stream, it is not available to be read again.
    /// A <see cref="StreamAlreadyReadException"/> will be thrown if the result is enumerated after it has reached
    /// the end of the stream.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the response.</typeparam>
    /// <seealso cref="IViewResult{T}" />
    internal class ViewResult<T> : IViewResult<T>
    {
        private readonly Result _result;

        public ViewResult(HttpStatusCode statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }

        public ViewResult(HttpStatusCode statusCode, string message, Stream responseStream, ISpan decodeSpan = null)
            : this(statusCode, message)
        {
            _result = new Result(responseStream, decodeSpan);
        }

        public HttpStatusCode StatusCode { get; }
        public string Message { get; }

        private MetaData _metaData;

        public MetaData MetaData
        {
            get
            {
                if (!_result.HasFinishedReading)
                {
                    // have to finish reading before using meta
                    throw new InvalidOperationException();
                }

                if (_metaData == null)
                {
                    _metaData = new MetaData {TotalRows = _result.TotalRows};
                }

                return _metaData;
            }
        }

        public IEnumerable<IViewRow<T>> Rows => _result;

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Intended for internal use only.</remarks>
        public bool ShouldRetry()
        {
            if (StatusCode == HttpStatusCode.OK)
            {
                return false;
            }

            // View status code retry strategy
            // https://docs.google.com/document/d/1GhRxvPb7xakLL4g00FUi6fhZjiDaP33DTJZW7wfSxrI/edit
            switch (StatusCode)
            {
                case HttpStatusCode.MultipleChoices: // 300
                case HttpStatusCode.MovedPermanently: // 301
                case HttpStatusCode.Found: // 302
                case HttpStatusCode.SeeOther: // 303
                case HttpStatusCode.TemporaryRedirect: //307
                case HttpStatusCode.RequestTimeout: // 408
                case HttpStatusCode.Conflict: // 409
                case HttpStatusCode.PreconditionFailed: // 412
                case HttpStatusCode.RequestedRangeNotSatisfiable: // 416
                case HttpStatusCode.ExpectationFailed: // 417
                case HttpStatusCode.BadGateway: // 502
                case HttpStatusCode.ServiceUnavailable: // 503
                case HttpStatusCode.GatewayTimeout: // 504
                    return true;
                case HttpStatusCode.NotFound: // 404
                    return !(Message.Contains("not_found") && (Message.Contains("missing") || Message.Contains("deleted")));
                case HttpStatusCode.InternalServerError: // 500
                    return !(Message.Contains("error") && Message.Contains("{not_found, missing_named_view}"));
                default:
                    return false;
            }
        }

        private class Result : IEnumerable<ViewRow<T>>, IDisposable
        {
            private static readonly JsonSerializer _jsonSerializer = new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            private readonly Stream _responseStream;
            private readonly ISpan _decodeSpan;
            private JsonTextReader _reader;
            private uint _totalRows;

            internal volatile bool HasFinishedReading;

            public Result(Stream responseStream, ISpan decodeSpan)
            {
                _responseStream = responseStream;
                _decodeSpan = decodeSpan;
            }

            public uint TotalRows
            {
                get
                {
                    ReadToRows();
                    return _totalRows;
                }
            }

            public IEnumerator<ViewRow<T>> GetEnumerator()
            {
                if (HasFinishedReading)
                {
                    throw new StreamAlreadyReadException();
                }

                // make sure we're are the 'rows' element in the stream
                if (ReadToRows())
                {
                    while (_reader.Read())
                    {
                        if (_reader.TokenType == JsonToken.StartObject)
                        {
                            yield return JToken.ReadFrom(_reader).ToObject<ViewRow<T>>(_jsonSerializer);
                        }
                    }
                }

                // we've reached the end of the stream, so mark it as finished reading
                HasFinishedReading = true;

                // if we have a decode span, finish it
                _decodeSpan?.Finish();
            }

            private bool ReadToRows()
            {
                // if reader is not null, we've already started to read the stream
                if (_reader != null)
                {
                    return true;
                }

                _reader = new JsonTextReader(new StreamReader(_responseStream));
                while (_reader.Read())
                {
                    if (_reader.Path == "total_rows" && _reader.TokenType == JsonToken.Integer)
                    {
                        uint totalRows;
                        if (uint.TryParse(_reader.Value.ToString(), out totalRows))
                        {
                            _totalRows = totalRows;
                        }
                    }
                    else if (_reader.Path == "rows" && _reader.TokenType == JsonToken.StartArray)
                    {
                        // we've reached the 'rows' element, return now so when we enumerate we start from here
                        return true;
                    }
                }

                // if we got here, there was no 'rows' element in the stream
                HasFinishedReading = true;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                if (_reader != null)
                {
                    _reader.Close(); // also closes underlying stream
                    _reader = null;
                }
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
