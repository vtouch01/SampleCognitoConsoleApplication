using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;


namespace SampleCognitoConsoleApplication
{
    class Program
    {
        private const string local_PoolID = "us-east-1_vt03v4vOn";
        private const string local_AppClientID = "1q0adp8cm6p32uke8psunvqdbi";

        private const string iin_PoolID = "us-east-1_GpEmQfi2A";
        private const string iin_AppClientID = "6244fbc8inolc4fko612osq37r";

        private static Amazon.RegionEndpoint Region = Amazon.RegionEndpoint.USEast1;
        private static AmazonCognitoIdentityProviderClient _provider;

        //API GATEWAY access
        //NOTE: Need to give admin access permissions to whatever iam role you create to run this trickle permission process (admin role)
        //**************
        private static string accessKeyId = "AKIAXYWEQ4RXFMYIJL5Z";
        private static string secretAccessKeyId = "ZdNRoy869J3aF8ozpgGlQKQS2ABuUi7vU+Zy9sEs";

        #region Constructor
        private static void CreateProviderClient()
        {
          
            _provider = new AmazonCognitoIdentityProviderClient(accessKeyId, secretAccessKeyId, Region);

        }

        #endregion

        static async Task ValidateUserInAWSCognito()
        {
            Console.WriteLine("This is user signing into the LC 2.0 ... ");
             Console.WriteLine("--------------------------");

            Console.WriteLine("Enter a username (email)");
            string username = Console.ReadLine();
            Console.WriteLine("Enter a password");
            string password = Console.ReadLine();
            //Console.WriteLine("Enter your email");
            //string email = Console.ReadLine();

            //create cognito provider client
            CreateProviderClient();

            //check if user is in the userpool
            var adminGetUserRequest = new AdminGetUserRequest()
                {
                    Username = username,
                    UserPoolId = iin_PoolID
                };

            try
            {
                Console.WriteLine("Checking if user is in AWS Cognito userpool ... ");
                var result = _provider.AdminGetUser(adminGetUserRequest);

               // if (result.HttpStatusCode == HttpStatusCodeUserNotFoundException)
                //***************
            }

            //TODO: make code specific for catch UserNotFoundException
            catch (Exception e )
            {
                Console.WriteLine("User is not in AWS Cognito userpool: " + iin_PoolID + " " + "and ClientID: " + iin_AppClientID + "\n");             
                Console.WriteLine();
                Console.WriteLine("Calling microservice.  Attempting migration into AWS User pool ...");
                Console.WriteLine("********************");
               
                try
                {
                    //*************************
                    //TODO: Have code here to check if the user is in Edlumina (again) *Need to work on checking password hash or relogging in*; 
                    // 

                    //if so, then create user in the AWS user pool
                    //**************************
                    Console.WriteLine("Rechecking user if exists in Edlumina LC 2.0 (call Cyanna service):");
                    Console.WriteLine("This user DOES exist in LC 2.0.  Creating User in iin userpool");
                    SetupUserInPool(username, password);
                }
                catch (Exception ex)
                {
                     //if not then return fail message
                    Console.WriteLine("User does not exist in user pool or existing system: " + username);
                    throw;
                }
                

            }
            
        }

        /// <summary>
        /// Setup User in AWS Cognito user pool
        /// 
        /// This is a two-step process
        /// 1) Create the user in the userpool with all the attributes and a temporary pwd
        /// 2) Sign in as the user so that you can set the user's permanent pwd
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="email"></param>
        /// <param name="password"></param>
        static void SetupUserInPool(string userName, string password)
        {
            //create the user
            //******************
            try
            {
                

                Console.WriteLine("Adding user profile attributes (ie: name) to the AWS Pool");
                
                Console.WriteLine("Adding user profile full name to the user account (ie: Danny Lam): ");

                var name = "Danny Lam";
                

                var createUserAttributes = new List<AttributeType>()
                    {
                        new AttributeType() {Name = "name", Value = name},
                        new AttributeType() {Name = "email", Value = userName},
                        new AttributeType() {Name = "email_verified", Value = "true"}
                    };

                AdminCreateUserRequest adminCreateUserRequest = new AdminCreateUserRequest()
                    {
                        Username = userName,
                        
                        UserPoolId = iin_PoolID,

                        //set the temporary password for this user in aws cognito
                        TemporaryPassword = password,

                        //suppress messages being delivered to the user
                        MessageAction = MessageActionType.SUPPRESS,
                        //DesiredDeliveryMediums = createUserSchema,
                        UserAttributes = createUserAttributes
                        
                        
                    };

                Console.WriteLine("Setup temporary pwd and create UUID for this user.");
                Console.WriteLine();

                var adminCreateUserResponse = _provider.AdminCreateUser(adminCreateUserRequest);
                
                //check if can create valid user or not
                if (adminCreateUserResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("Successful AdminCreateUser for migrating user: " + userName);
                    //initiate authorization request to the server 
                    try
                    {
                        ////Now sign in the migrated user to set the permanent password and confirm the user                      
                        Console.WriteLine("Relogin the new created user on AWS Cognito. Set the user's permanent password (NO_SRP_AUTH)");
                        AdminInitiateAuthRequest adminInitiateAuthRequest = new AdminInitiateAuthRequest
                        {
                            ClientId = iin_AppClientID,
                            UserPoolId = iin_PoolID,
                            AuthFlow = AuthFlowType.ADMIN_NO_SRP_AUTH

                        };

                        //passing the username and password directly
                        adminInitiateAuthRequest.AuthParameters.Add("USERNAME", adminCreateUserResponse.User.Username);
                        adminInitiateAuthRequest.AuthParameters.Add("PASSWORD", password);
                        
                        
                        //Note: this will set the username to be a UUID
                        var adminInitiateAuthResponse = _provider.AdminInitiateAuth(adminInitiateAuthRequest);
                        
                            if (adminInitiateAuthResponse.ChallengeName != "NEW_PASSWORD_REQUIRED")
                            {
                                //unexpected challenge name - log and exit
                                Console.WriteLine("Unexpected challenge name after adminInitiateAuth (" + adminInitiateAuthResponse.ChallengeName + "), migrating user created, but password not set");
                                
                            }
                            else
                            {
                                //Set the userid/password

                                var challengeResponses = new Dictionary<string, string>();
                                challengeResponses.Add("NEW_PASSWORD", password);

                                //get the UUID 
                                challengeResponses.Add("USERNAME", adminInitiateAuthResponse.ChallengeParameters["USER_ID_FOR_SRP"]);
                                        

                        
                                AdminRespondToAuthChallengeRequest adminRespondToAuthChallengeRequest = new AdminRespondToAuthChallengeRequest
                                {
                                    ClientId = iin_AppClientID,
                                    UserPoolId = iin_PoolID,
                                    ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
                                    Session = adminInitiateAuthResponse.Session,
                                    ChallengeResponses = challengeResponses
                                  
                                };

                                var adminRespondToAuthChallengeResp =
                                    _provider.AdminRespondToAuthChallenge(adminRespondToAuthChallengeRequest);

                                if (adminRespondToAuthChallengeResp.AuthenticationResult != null)
                                {
                                    Console.WriteLine("Successful response from RespondtoAuthChallenge.   Setup new user account on AWS Cognito");
                                    Console.WriteLine();
                                    Console.WriteLine("REDIRECT back to Edlumina LC 2.0 prompt for user to login again");

                                }
                            }
                        
                        

                    }
                    catch (Exception ext)
                    {
                        { }
                        throw;
                    }



                }
            }
            catch (Exception e)
            {
                throw;

            }

        //*****************************
           




        }


        #region Local Methods

        static async Task SignUpUser()
        {
            Console.WriteLine("Enter a username");
            string username = Console.ReadLine();
            Console.WriteLine("Enter a password");
            string password = Console.ReadLine();
            Console.WriteLine("Enter your email");
            string email = Console.ReadLine();


            AmazonCognitoIdentityProviderClient provider = new AmazonCognitoIdentityProviderClient(new Amazon.Runtime.AnonymousAWSCredentials(), Region);

            SignUpRequest signUpRequest = new SignUpRequest()
            {
                ClientId = local_AppClientID,
                Username = username,
                Password = password

            };

            List<AttributeType> attributes = new List<AttributeType>()
                {
                    
                    new AttributeType(){Name = "email", Value = email},
                    new AttributeType(){Name = "custom:Data", Value="Data"}
                };

            signUpRequest.UserAttributes = attributes;

            try
            {
                SignUpResponse result = await provider.SignUpAsync(signUpRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine("User creation Failed. " + e.Message + "\n");
                return;



            }

            Console.WriteLine("User creation successful!");

        }


        static async Task SignInUser()
        {
            Console.WriteLine("Enter your username");
            string username = Console.ReadLine();
            Console.WriteLine("Enter your password");
            string password = Console.ReadLine();


            AmazonCognitoIdentityProviderClient provider = new AmazonCognitoIdentityProviderClient(new Amazon.Runtime.AnonymousAWSCredentials(), Region);

            //CognitoUserPool userPool = new CognitoUserPool(local_PoolID, local_AppClientID, provider);

            //CognitoUser user = new CognitoUser(username, local_AppClientID, userPool, provider);

            CognitoUserPool userPool = new CognitoUserPool(iin_PoolID, iin_AppClientID, provider);

            CognitoUser user = new CognitoUser(username, iin_AppClientID, userPool, provider);

            InitiateSrpAuthRequest authRequest = new InitiateSrpAuthRequest()
            {
                Password = password

            };

            AuthFlowResponse authResponse = null;
            try
            {
                authResponse = await user.StartWithSrpAuthAsync(authRequest).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine("Login Failed: {0}\n", e.Message);
                return;
            }

            GetUserRequest getUserRequest = new GetUserRequest();
            getUserRequest.AccessToken = authResponse.AuthenticationResult.AccessToken;

            var keepWorking = true;

            while (keepWorking)
            {
                GetUserResponse getUser = await provider.GetUserAsync(getUserRequest);
               

                var dataStr = getUser.UserAttributes.Where(a => a.Name == "custom:Data").First().Value;
                //int data = Convert.ToInt32(dataStr);

                Console.WriteLine("Welcome, {0}. The data string in here is {1}.", username, dataStr);
                Console.WriteLine(" 1. Append to data string");
                Console.WriteLine(" 2. Update data string with new value");
                Console.WriteLine("3. Exit");

                string selection = Console.ReadLine();
                var sel = Convert.ToInt32(selection);
                if (sel != 1 && sel != 2)
                    return;

                Console.WriteLine("Enter new value: ");
                var valueStr = Console.ReadLine();

                AttributeType attributeType = new AttributeType()
                {
                    Name = "custom:Data",
                    Value = (sel == 1 ? dataStr + valueStr : valueStr)

                };

                UpdateUserAttributesRequest updateUserAttributesRequest = new UpdateUserAttributesRequest()
                {
                    AccessToken = authResponse.AuthenticationResult.AccessToken

                };
                updateUserAttributesRequest.UserAttributes.Add(attributeType);

                provider.UpdateUserAttributes(updateUserAttributesRequest);
            }

        }

        #endregion

        static void Main(string[] args)
        {
            var keepWorking = true;
            while (keepWorking)
            {
                Console.WriteLine("Welcome!\n 1)Sign up new user\n  2)Sign in existing user\n 3)validate user in AWS User pool  4)exit S");
                var selection = Console.ReadLine();
                int sel = Convert.ToInt32(selection);
                
                switch (sel)
                {
                    case 1:                       
                        SignUpUser().Wait();
                        break;
                    case 2:
                        SignInUser().Wait();
                        break;
                    case 3:
                        ValidateUserInAWSCognito().Wait();
                        break;
                    default:
                        keepWorking = false;
                        break;

                }

                
            }
        }
    }
}
