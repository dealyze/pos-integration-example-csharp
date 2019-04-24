using System;
using System.Threading;
using Quobject.SocketIoClientDotNet.Client;
using Newtonsoft.Json.Linq;

namespace DealyzeWebsocketTest2
{
    class MainClass
    {
        private static Socket socket { get; set; }

        public static void Main(string[] args)
        {
            connect();

            while(true) {
                Thread.Sleep(100);
            }
        }

        private static void connect()
        {
            // initialize the socket
            Console.WriteLine("searching for dealyze...");
            socket = IO.Socket("ws://localhost:3100");

            // EVENT_CONNECT occurs when we successfully esablish a connection to dealyze
            socket.On(Socket.EVENT_CONNECT, () =>
            {
                Console.WriteLine("connected");

            });

            // EVENT_DISCONNECT occurs when dealyze shuts down or restarts
            socket.On(Socket.EVENT_DISCONNECT, (data) =>
            {
                string reason = data as string;
                Console.WriteLine("disconnected " + reason);

                // the disconnection was initiated by the server, you need to reconnect manually
                if (reason as string == "io server disconnect")
                {
                    connect();
                }
            });

            // customer occurs when a cusomer has signed in or when there's an error
            socket.On("customer", (data) =>
            {
                JObject json = data as JObject;
                if (json["error"] != null)
                {
                    Console.WriteLine("customer: " + json["error"]);
                    return;
                }

                Console.WriteLine("customer signed in with phone number " + json["customer"]["phoneNumber"]);

                payBill(json["customer"]);
            });

            // order occurs when a redemption or a reward is taking place
            // in the future order may also be called during the redemption of a promotion
            socket.On("order", (data) =>
            {
                JObject json = data as JObject;
                if (json["error"] != null)
                {
                    Console.WriteLine("order: " + json["error"]);
                    return;
                }

                JArray discounts = json["order"]["discounts"] as JArray;
                if (discounts != null && discounts.Count > 0)
                {
                    JToken discount = discounts[0];
                    Console.WriteLine("order received with " + discount["name"]);
                    redeemReward(json["order"]);
                }
            });
        }

        /*
         * confirmRedemption prompts the user to confirm the reward redemption
         */
        private static void redeemReward(JToken order)
        {
            if (order == null)
            {
                return;
            }

            Console.WriteLine("approve the redeemption of " + order["discounts"][0]["name"] + "? [yes/no/cancel]: ");
            var answer = Console.ReadLine();

            // return to the menu
            answer = answer.ToLower();
            if (answer == "cancel")
            {
                return;
            }

            // to redeem the reward, add the relevant line items
            // and send it back to Dealyze
            if (answer == "yes")
            {
                var responseOrder = order;
                JToken discount = order["discounts"][0] as JToken;

                var items = new JArray();
                items.Add(discount["name"]);
                items.Add(discount["skus"]);
                responseOrder["items"] = items;
                responseOrder["total"] = 0;

                JObject response = new JObject();
                response["order"] = order;
                response["employee"] = testEmployee();
                socket.Emit("order", response.ToString());

                Console.WriteLine("approved redemption");
                return;
            }

            // to cancel the redemption remove the discount from the order
            // and send it back to Dealyze
            if (answer == "no")
            {
                var responseOrder = order;
                JArray discounts = order["discounts"] as JArray;
                discounts.RemoveAt(0);
                responseOrder["discounts"] = discounts;

                socket.Emit("order", responseOrder.ToString());
                Console.WriteLine("redemption canceled");
                return;
            }

            // bad input
            Console.WriteLine("you must enter 'yes', 'no', or 'cancel'");
        }

        /*
         * payBill prompts the employee for a bill payment
         */
        private static void payBill(JToken customer)
        {
            if (customer == null)
            {
                return;
            }

            Console.WriteLine("how many bills did the customer pay ? [integer > 0/cancel]:");
            var answer = Console.ReadLine();

            if (answer == "cancel")
            {
                Console.WriteLine("bill payment cancelled");
                return;
            }

            int count = 0;
            if (!Int32.TryParse(answer, out count))
            {
                Console.WriteLine("you must enter a number > 0");
                return;
            }

            // send the bill payment order with the current employee information
            JArray skus = new JArray();
            skus.Add("abc123");

            JObject item = new JObject();
            item["name"] = "Bill Pay";
            item["skus"] = skus;

            JArray items = new JArray();
            items.Add(item);

            JObject order = new JObject();
            order["items"] = items;

            JObject response = new JObject();
            response["employee"] = testEmployee();
            response["order"] = order;

            socket.Emit("order", response);

            Console.WriteLine("" + count + " bill" + (count > 1 ? "s" : "") + " paid");
        }

        private static JObject testEmployee()
        {
            JObject employee = new JObject();
            employee["code"] = "123456";
            employee["firstName"] = "Bob";
            employee["lastName"] = "Johnson";
            employee["emailAddress"] = "bob@dealyze.com";
            return employee;
        }
    }
}
