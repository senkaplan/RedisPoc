namespace RedisPoc
{
    using System;
    using StackExchange.Redis;
    using System.Globalization;
    using System.Linq;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using System.Text.RegularExpressions;

    //Singleton pattern for redis cache
    public class RedisCache
    {
        //Connection string. For all available configuration options, https://stackexchange.github.io/StackExchange.Redis/Configuration.html
        const string connectionString = "127.0.0.1:6379,password=kaplan@123,connectRetry=3,defaultDatabase=3";

        //lock object to be used for thread synchronization.
        private static readonly object lockObj = new object();

        //RedisCacheHelper Instance
        private static RedisCache _redisCacheHelper = null;

        //json serializer settings
        private static JsonSerializerSettings stringSerializerSetting = new JsonSerializerSettings()
        {
            MaxDepth = 3,
        };

        private static CultureInfo cultureInfo = new CultureInfo("en-US");

        /// <summary>
        /// Property to set and get the serializer settings for Json serialization
        /// </summary>
        public static JsonSerializerSettings StringSerializerSetting
        {
            set => stringSerializerSetting = value;
            get =>  stringSerializerSetting;
        }
        
        /// <summary>
        /// Property to set and get the culture Info
        /// </summary>
        public static CultureInfo FormatCulture
        {
            set => cultureInfo = value;
            get => cultureInfo;
        }

        /// <summary>
        /// Property to get the connection object.
        /// </summary>
        public static ConnectionMultiplexer Connection { get; } = ConnectionMultiplexer.Connect(connectionString);

        /// <summary>
        /// RedisCacheHelper instance property
        /// </summary>
        public static RedisCache Instance
        {
            get
            {
                lock (lockObj)
                {
                    if (_redisCacheHelper == null)
                    {
                        _redisCacheHelper = new RedisCache();
                    }
                }

                return _redisCacheHelper;
            }
        }



        /// <summary>
        /// private constructor to prevent instance creation outside the class.
        /// </summary>
        private RedisCache()
        {
        }


        #region String cache

        //Simple key value pair of strings.
        //Also used in cases when we have to get or set an entire object all the time. Here we use Json serialization (Use protobuf for performance improvement).
        //Since this type is binary safe, any type of string can be saved without a fear of a terminating character. Hence, binary serialization can also be used with sufficient modification. 
        //Types of strings and operations: 
        //1) Numeric string (eg: "310", "99.79",...). Permitted operations include atomic counters for increment and decrement (high speed atomic operations) with one command to the server.
        //2) Strings: Allows to slice and dice the strings and retrieve parts of it. Index can be calculated from the end or the start of the string. 
             //You can also append to or overwrite parts of an existing strings without a read/write/update cycle.
        //3) Bit Strings: Since String type is binary safe, you can have a bits datatype to record the statuses of n events or operations or users. Bit level operations are supported 
            //eg: Number of set bits in the string (number of logged in users).
            //Using Bits datatype is very efficient in performance. eg: daily login statuses of ten million users will take as less as 1.2mb

        /// <summary>
        /// Generic method to asynchronously cache a string value for a key. 
        /// Uses Json serialization.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="value">The value as an object</param>
        /// <param name="timeSpanSeconds">The time for which the cache has to be preserved</param>
        public static void SetCacheString<T>(string key, T value, int timeSpanSeconds)
        {
            string cacheValue = JsonConvert.SerializeObject(value);
            IDatabase db = Connection.GetDatabase();
            db.StringSetAsync(key, cacheValue, TimeSpan.FromSeconds(timeSpanSeconds), When.Always, CommandFlags.FireAndForget);
        }

        /// <summary>
        /// Generic method to get the cached value for a given key
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <returns>Returns the cached value for the key.</returns>
        public static T GetCacheString<T>(string key)
        {
            IDatabase db = Connection.GetDatabase();
            string cachedValue = db.StringGet(key);
            return JsonConvert.DeserializeObject<T>(cachedValue); //Us culture info for format 
        }

        #endregion


        #region Hash Data cache

        //Stores a set of hash key value pairs.
        //Used in cases where we have to set and get individual fields of a large object rather than accessing the ncomplete object.
        //Does not directly support complex values. But can be used to point to other hash values. 
        //Atomic increment of numerical values is possible.
        //Performance suffers if used for saving and retrieving an entire object using this data type.
        //Should only be used if the number of hashes is less than 100.
        //However due to the design of redis, it is more efficient to have 100 hashes with 100 internal keys that 10,000 top level keys pointing to string values.


        /// <summary>
        /// Generic Method to asynchronously cache a hashset value for a key.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="value">The value as an object</param>
        /// <param name="timeSpanSeconds">The time for which the cache has to be preserved</param>
        public static void SetCacheHash<T>(string key, T value, int timeSpanSeconds)
        {
            IDatabase db = Connection.GetDatabase();
            HashEntry[] hashset = value.GetType().GetProperties()
                .Where(x => x.GetValue(value) != null)
                .Select(prop => new HashEntry(prop.Name, Stringify(prop.GetValue(value))))
                .ToArray();

            db.HashSet(key, hashset); //Synchronized setting of hashes.
            db.KeyExpireAsync(key, TimeSpan.FromSeconds(timeSpanSeconds), CommandFlags.FireAndForget);
        }

        /// <summary>
        /// Generic Method to asynchronously set or change a hashset field value for a key.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="field">The hash field</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="timeSpanSeconds">The time for which the cache has to be preserved</param>
        public static void SetCacheHashField<T>(string key, string field, T value, int timeSpanSeconds)
        {
            IDatabase db = Connection.GetDatabase();

            db.HashSet(key, field, JsonConvert.SerializeObject(value), When.Always); //Synchronized setting of a field in hash cache.
            db.KeyExpireAsync(key, TimeSpan.FromSeconds(timeSpanSeconds), CommandFlags.FireAndForget);
        }



        /// <summary>
        /// Method to get the cached hashset for a given key.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <returns>Returns the cached hashset for the given key</returns>
        public static T GetCacheHash<T>(string key)
        {
            IDatabase db = Connection.GetDatabase();
            Dictionary<string, string> kvPairs = db.HashGetAll(key).ToDictionary(pair => pair.Name.ToString(), pair => pair.Value.ToString());
            return DeserializeKeyValuePairs<T>(kvPairs);
        }

        /// <summary>
        /// Method to get the cached hashset field value for a given key .
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="field">The field</param>
        /// <returns>Returns the cached hashset field value for the key and the field name.</returns>
        public static T GetCacheHashField<T>(string key, string field)
        {
            IDatabase db = Connection.GetDatabase();
            return DeserializeRedisValue<T>(db.HashGet(key, field));
        }

        #endregion


        #region List Cache

        // Lists: Acts like a linked list, Stack and Queue at the same time(operations supported). Can be traversed from head or tail.
        // Allows insertion at a specified index with a cost on performance.
        // Allows duplicates. 
        // Also can maintain a fixed-length lists by trimming the list to a specific size after every insertion.


        #region Queue

        //Queue implementation using Redis list.
        //Can also be used for messaging Queue implementation.
        //Below implementation adds strings and stringified Json.


        /// <summary>
        /// Method to add a value to the queue. Creates the queue, if it doesn't exist.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="value">The value to be added to the Queue</param>
        /// <param name="timeSpanSeconds">The time for which the cache has to be preserved</param>
        public static void SetCacheQueue<T>(string key, T value, int timeSpanSeconds)
        {
            IDatabase db = Connection.GetDatabase();
            db.ListRightPush(key, Stringify<T>(value), When.Always); //Add value to the end of the list
            db.KeyExpireAsync(key, TimeSpan.FromSeconds(timeSpanSeconds), CommandFlags.FireAndForget); //Set expire time.
        }


        /// <summary>
        /// Method to add a range of values to the queue. Creates the queue, if it doesn't exist.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="value">The array of values to be added to the Queue</param>
        /// <param name="timeSpanSeconds">The time for which the cache has to be preserved</param>
        public static void SetCacheQueueRange<T>(string key, T[] values, int timeSpanSeconds)
        {
            IDatabase db = Connection.GetDatabase();
            db.ListRightPush(key, values.Select(x => (RedisValue)Stringify<T>(x)).ToArray()); //Add a range of values to the end of the list.
            db.KeyExpireAsync(key, TimeSpan.FromSeconds(timeSpanSeconds), CommandFlags.FireAndForget); //Set expire time
        }

        /// <summary>
        /// Method to get the first element from a queue for a key
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="erase">A flag to decide whether to erase the values</param>
        /// <returns>Returns the first element from the queue</returns>
        public static T GetCacheQueue<T>(string key, bool erase)
        {
            IDatabase db = Connection.GetDatabase();
            RedisValue value = new RedisValue();

            if (erase)
            {
                value = db.ListLeftPop(key); //Synchronously pop(get and delete) the first element 
            }
            else
            {
                value = db.ListRange(key,0,0).FirstOrDefault(); //Synchronously get the first element 
            }

            return DeserializeRedisValue<T>(value) ; //Deserialize element
        }

        /// <summary>
        /// Method to get a queue range for a key
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="key">The key</param>
        /// <param name="count">The number of list items to get</param>
        /// <param name="erase">A flag to decide whether to erase the values</param>
        /// <returns>Returns the first 'count' elements from the queue</returns>
        public static T[] GetCacheQueueRange<T>(string key, int count, bool erase)
        {
            IDatabase db = Connection.GetDatabase();
            RedisValue[] values = db.ListRange(key, 0, count - 1);

            //If erase, then delete the elements from the list.
            if (erase)
            {
                db.ListTrim(key, count,-1); //Synchronously trim the elements from 0 to count - 1 
            }

            return (values.Select(x => DeserializeRedisValue<T>(x)).ToArray()); //get top elements from 0 to count -1
        }

        #endregion


        #region Stack

        ///A stack implementation can also be achieved using the methods specified above.
        ///For a stack, it is always LeftPush and LeftPop

        #endregion


        #endregion


        #region Sets
        /// A set is used to store unique unordered strings. If the same element is added twice, it will only show up once.
        /// Sets are used for saving space and time when the only purpose is to check for existence of a value.  
        /// Sets are faster than lists for membership checking, insertion, and deletion of members.
        /// It also supports set operations like union, intersection, and difference of multiple sets at once.
        /// Unlike lists, the time complexity for membership checks is a constant, O(1).
        /// Also allows random selection of values(with or without removal and replacement.)

        #endregion


        #region Sorted Sets

        /// They are actually a combination of sets(unique items) and skip lists(a combination of Arrays and Linked List)
        /// The sort order is defined by element's score.
        /// The time complexity for search and insertion is O(log n)

        #endregion


        #region Common

        /// <summary>
        /// Method to stringify objects
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="value">The value to be stringified</param>
        /// <returns>Returns the stringified json equivalent </returns>
        private static string Stringify<T>(T value)
        { 
            return value.GetType() == typeof(string) ? value.ToString() : JsonConvert.SerializeObject(value,typeof(T), stringSerializerSetting);
        }
        
        /// <summary>
        /// Method to deserialize a key value pair to an object.
        /// </summary>
        /// <typeparam name="T">The return object type</typeparam>
        /// <param name="kvPairs">The key value pair dictionary</param>
        /// <returns>Returns the deserialized object</returns>
        private static T DeserializeKeyValuePairs<T>(Dictionary<string, string> kvPairs)
        {
            string cachedValue = Stringify(kvPairs); //Stringify Keyvalue pairs.

            //Formatting json for correcting the changes introduced during serialization from the above line
            cachedValue = Regex.Unescape(cachedValue.Replace(":\"{", ":{", true, cultureInfo)  //:"{ to :{
                .Replace("}\",", "},", true, cultureInfo)  //}", to },
                .Replace("}\"}", "}}", true, cultureInfo)  //}"} to }}
                .Replace(":\"[", ":[", true, cultureInfo)  //:"[ to :[
                .Replace("]\",", "],", true, cultureInfo)  //]", to ],
                .Replace("]\"}", "]}", true, cultureInfo));//]"} to ]}

            return (T)Convert.ChangeType(JsonConvert.DeserializeObject<T>(cachedValue), typeof(T), cultureInfo); //Us culture info for format 
        }


        /// <summary>
        /// Method to deserialize RedisValue object.
        /// </summary>
        /// <typeparam name="T">The return object type</typeparam>
        /// <param name="redisValue">the RedisValue object to be deserialized.</param>
        /// <returns>Returns the deserialized object</returns>
        private static T DeserializeRedisValue<T>(RedisValue redisValue)
        {
            if (redisValue.HasValue)
            {
                return typeof(T) == typeof(String) ? (T)(object)redisValue.ToString() : JsonConvert.DeserializeObject<T>(redisValue);
            }
            else
            {
                return default(T);
            }
        }

        #endregion

    }
}
