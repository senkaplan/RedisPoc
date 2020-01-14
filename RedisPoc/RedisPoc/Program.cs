namespace RedisPoc
{
    using System;
    using System.Collections.Generic;

    class Program
    {
        static void Main()
        {


            string redisConnString = "127.0.0.1:6379,password=kaplan@123,connectRetry=3,defaultDatabase=0"; //Redis connection string
            RedisCache.Connect(redisConnString); // Connect to the redis server.

            #region stringcache

            Test1 test1 = new Test1()
            {
                value1 = 59,
                value2 = "string test",
                value3 = new List<Test3>() { new Test3() { value1 = 46, value2 = "ashdgj" }, new Test3() { value1 = 78, value2 = "sudjha" } },
                Testenum = TestEnum.value2,
                value4 = "asjda",
                valuepubliclist = new List<Test3>() { new Test3() { value1 = 46, value2 = "tuyrio" }, new Test3() { value1 = 78, value2 = "idfdkjsd" } },
                ValTup = new Tuple<string, string>("asdhjadja", "kjrjkrk")
            };

            test1.ValDict = new Dictionary<string, string>();
            test1.ValDict.Add("test111", "test3");
            test1.ValDict.Add("test1343", "ssadasdasa");
            
            RedisCache.SetCacheString<Test1>("key1", test1, 28800); //28800 seconds = 8 hours
            var test1_res = RedisCache.GetCacheString<Test1>("key1");

            #endregion

            #region Hash Data Cache

            //Testing hash. A very complex object with all types of fields and variables.
            RedisCache.SetCacheHash("key5", test1, 28800); //28800 seconds = 8 hours
            var tes = RedisCache.GetCacheHash<Test1>("key5");

            //Testing hash. Nested object.
            Test2 test2 = new Test2() { value2 = new Test3() { value1 = 25, value2 = "starting" }, valString = "TestingStringHashField", value1 = 90 };
            RedisCache.SetCacheHash<Test2>("key6", test2, 28800); //28800 seconds = 8 hours
            RedisCache.SetCacheHashField<Test3>("key6", "value2", new Test3() { value1 = 50, value2 = "Testing" }, 28800); //Overwriting value2 field in the key6 hash cache. Note the type of the generic used here.
            var value2 = RedisCache.GetCacheHash<Test2>("key6");
            //Testing hash. Different values for the same field in the same hash. Saving a version 2 for a particular field.
            RedisCache.SetCacheHashField<Test3>("key6", "value3", new Test3() { value1 = 100, value2 = "Testing100" }, 28800); //adding value3 field to the key6 hash cache. Note the type of the generic used here.
            var value3 = RedisCache.GetCacheHash<Test2>("key6");
            var value3_new = RedisCache.GetCacheHashField<Test3>("key6", "value3"); //get value3 field in the key6 hash cache. Note the type of the generic used here.
            var value2_string = RedisCache.GetCacheHashField<string>("key6", "valString"); //get value3 field in the key6 hash cache. Note the type of the generic used here.


            //Testing hash. Array inside object.
            Test4 test4 = new Test4() { value1 = 100, value2 = new string[] { "Name", "Name1", "Name2" } };
            RedisCache.SetCacheHash<Test4>("key7", test4, 28800); //28800 seconds = 8 hours
            var value4 = RedisCache.GetCacheHash<Test4>("key7");

            //Testing hash. List inside object.
            Test5 test5 = new Test5() { value1 = 100, value2 = new List<string> { "Name", "Name1", "Name2" } };
            RedisCache.SetCacheHash<Test5>("key8", test5, 28800); //28800 seconds = 8 hours
            var value5 = RedisCache.GetCacheHash<Test5>("key8");

            #endregion

            #region List 

            #region Queue

            //Test Queue. String Arrays
            RedisCache.SetCacheQueue("key9", "test0", 28800);
            RedisCache.SetCacheQueueRange("key9", new string[] { "test1", "test2", "test3", "test4", "test5" }, 28800);
            string list = RedisCache.GetCacheQueue<string>("key9", false);
            string list_0 = RedisCache.GetCacheQueue<string>("key9", true);
            string[] list_1 = RedisCache.GetCacheQueueRange<string>("key9", 2, false);
            string[] list_2 = RedisCache.GetCacheQueueRange<string>("key9", 2, true);


            //Test Queue. Object Arrays
            RedisCache.SetCacheQueue("key10", test1, 28800);
            RedisCache.SetCacheQueueRange("key10", new Test1[] { test1, test1, test1 }, 28800);
            Test1 list_obj = RedisCache.GetCacheQueue<Test1>("key10", false);
            Test1 list_0_obj = RedisCache.GetCacheQueue<Test1>("key10", true);
            Test1[] list_1_obj = RedisCache.GetCacheQueueRange<Test1>("key10", 2, false);
            Test1[] list_2_obj = RedisCache.GetCacheQueueRange<Test1>("key10", 2, true);


            //Test Queue. Nested Object Arrays
            RedisCache.SetCacheQueue("key11", test1, 28800);
            RedisCache.SetCacheQueueRange("key11", new Test1[] { test1, test1, test1 }, 28800);
            Test1 list_obj_nested = RedisCache.GetCacheQueue<Test1>("key11", false);
            Test1 list_0_obj_nested = RedisCache.GetCacheQueue<Test1>("key11", true);
            Test1[] list_1_obj_nested = RedisCache.GetCacheQueueRange<Test1>("key11", 2, false);
            Test1[] list_2_obj_nested = RedisCache.GetCacheQueueRange<Test1>("key11", 2, true);

            #endregion

            #endregion

        }

    }


    public class Test1
    {
        public int value1 { get; set; }  
        public string value2 { get; set; }
        public TestEnum Testenum { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Testing")]
        public List<Test3> value3 { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Testing")]
        public List<Test3> valuepubliclist;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Testing")]
        public string value4;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Testing")]
        public Dictionary<string, string> ValDict { get; set; }

        public Tuple<string, string> ValTup { get; set; }

    }

    public enum TestEnum
    {
        value1,
        value2
    }


    public class Test2
    {
        public int value1 { get; set; } 
        public string valString { get; set; }
        public Test3 value2 { get; set; }
    }

    public class Test3
    {
        public int value1 { get; set; } 
        public string value2 { get; set; }
    }


    public class Test4
    {
        public int value1 { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Testing")]
        public string[] value2 { get; set; }
    }


    public class Test5
    {
        public int value1 { get; set; } 

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Testing")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Testing")]
        public List<string> value2 { get; set; }
        //= new List<string> { "Name", "Name1", "Name2" };
    }

}
