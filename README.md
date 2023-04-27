# Installation

1. Import the base network code into the game.

- Create an object to hold Network scripts.
- place NetSocket and CNetItemTracker and CNetBPS(optional, shows BPS speed display) on.
- Put three copies of CNetGraph in. Color each one with simple color materials. It should look like this:
![](https://i.imgur.com/pkkqlQJ.png)
- Don't worry about the item list as we do not spawn any objects yet.

2. Add the player scripts to PlayerCharacter in Base scene.

- as a common step, place CNetId on the Player object. This will enable identification of the player character and other objects.
- Additionally, place CNetInfo, CNetCharacter, and CNetAnimatorMonitor into the player object.
- copy the two graphs (forward and horizontal movement) from the standard 'animatormonitor' to the CNetAnimatorMonitor.
- remove the old animatormonitor from the player object.
- Make sure CNet Identifier is running and the other scripts are disabled to start. It should look like this:
![](https://i.imgur.com/KzANWOE.png)

3. Add the player scripts to the Player prefab in the Characters Manager under Systems.

- Do the same as before, but this time on the prefab. It should look like this:
![](https://i.imgur.com/IY7YdPm.png)


4. Modify UMACharacterBuilder.cs:
- add `using CNet;`

- around line 217, modify the code so it looks like this:
```
            if (m_AddHealth)
            {
                data.gameObject.AddComponent<Opsive.UltimateCharacterController.Traits.AttributeManager>();
                CNet.CNet.BuildHealthMonitors(data.gameObject);
                data.gameObject.AddComponent<Opsive.UltimateCharacterController.Traits.CharacterHealth>();
                data.gameObject.AddComponent<Opsive.UltimateCharacterController.Traits.CharacterRespawner>();
            }
```

- add the following at line 1046:
```
                CNet.CNet.BuildPlayer(gameObject);
```

5. Modify Env/Clock.cs:
- add the following to Start():
```
            CNet.CNet.SetupClock( this.SetHour, this.SetDaySpeed );
```


6. Copy CNetConnect.cs to the Assets/_IMPUNES/Multiplayer directory.

7. Modify CharactersManager.cs:
- add the following to Start():
```
             CNet.SetupCharacterManager(this.CreateRemoteAvatar, this.CreateNPC);
```
- make sure the following 3 functions are setup:

```
        public GameObject CreateRemoteAvatar(Vector3 position, float heading)
        {
            var dynamicCharacterAvatar = CreateDynamicCharacter(position, heading, CharacterFeatures.Full, CharactersManager.CharacterQuality.Full);
            if (!dynamicCharacterAvatar) {
                Debug.Log("NewUser rejected");
                return null;
            }

            var characterBuilder = dynamicCharacterAvatar.GetComponent<UMACharacterBuilder>();
            //characterBuilder.m_Build = false;
            characterBuilder.m_AIAgent = true;
            characterBuilder.m_AssignCamera = false;
            characterBuilder.m_AddItems = true;

            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            dynamicCharacterAvatar.BuildCharacter();

            return dynamicCharacterAvatar.gameObject;
        }

        public GameObject CreateNPC(Vector3 position, float heading)
        {
            var dynamicCharacterAvatar = CreateDynamicCharacter(position, heading, CharacterFeatures.Full, CharactersManager.CharacterQuality.Full);
            if (!dynamicCharacterAvatar) {
                Debug.Log("NewUser rejected");
                return null;
            }
            RandomizeCharacterAvatar(dynamicCharacterAvatar, maleAverage);

            var characterBuilder = dynamicCharacterAvatar.GetComponent<UMACharacterBuilder>();
            //characterBuilder.m_Build = false;
            characterBuilder.m_AIAgent = true;
            characterBuilder.m_AssignCamera = false;
            characterBuilder.m_AddItems = true;

            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            dynamicCharacterAvatar.BuildCharacter();

            return dynamicCharacterAvatar.gameObject;
        }

        public GameObject CreateCharacterRandom(Vector3 position, float heading, CharacterFeatures features = CharacterFeatures.Pedestrian, CharacterQuality quality = CharacterQuality.NotImportant)
        {
            var dynamicCharacterAvatar = CreateDynamicCharacter(position, heading, features, quality);
            if (!dynamicCharacterAvatar) return null;

            RandomizeCharacterAvatar(dynamicCharacterAvatar, maleAverage);

            Application.backgroundLoadingPriority = ThreadPriority.Low;

            //testAvatar = dynamicCharacterAvatar;
            //UMAAssetIndexer.Instance.Preload(testAvatar, true).Completed += Avatar_Completed;
            dynamicCharacterAvatar.BuildCharacter();

            Application.backgroundLoadingPriority = ThreadPriority.Low;

            return dynamicCharacterAvatar.gameObject;
        }
```

8. Copy in the new VehicleSource.cs and UseVehicle.cs files.

9. Setup the vehicle with the CNetVehicle and CNetId classes. It should look like this:
![](https://i.imgur.com/qMH0wyA.png)


10. Modify Opsive's DetectObjectAbilityBase.cs at line 98 to make the following property virtual:
```
        public virtual GameObject DetectedObject { get { return m_DetectedObject; }
```

11. Add CNet to the assembly definition for IMPUNES.

12. I don't remember exactly how to configure this, but your scripting define symbols should look like this:
![](https://i.imgur.com/vXkrDkI.png)

# MinGW Instructions:

- clone to c:\serv
- delete CMakeLists.txt and copy CMakeWin.txt to CMakeLists.txt
- install msys from https://www.msys2.org/
- start a UCRT64 shell.
- pacman -S mingw-w64-x86_64-cmake mingw-w64-x86_64-c++ mingw-w64-x86_64-make
- optionally, pacman -S mingw-w64-x86_64-gdb
- switch to the MINGW64 shell.
- download sol2 to c:\serv\sol
- go to c:\serv\sol and create a 'build' directory.
- from the build directory:
- `cmake ..`
- `mingw32-make` (should download and install lua)
 (this builds target liblua-5.4.4 in build/x64/lib and build/x64/bin)
- locate liblua-5.4.4.dll.a and liblua-5.4.4.dll and copy to c:\serv
- go to c/serv and create a 'debug' directory'
- copy the .dll file to the debug dir
- build the project in 'debug' directory using `cmake ..`
- `mingw32-make` (will build server.exe)
- you're set to run server.exe

