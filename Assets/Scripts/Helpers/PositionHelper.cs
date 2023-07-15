using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Helpers
{
    public class PositionHelper
    {
        /// <summary>
        /// Gets the position of an object in a line of objects.
        /// </summary>
        /// <param name="maxLength">Total length of the line.</param>
        /// <param name="objectLength">Length of a single object in the direction of the line.</param>
        /// <param name="index">Index of the position in the line.</param>
        /// <param name="count">Total number of objects in the line.</param>
        /// <param name="marginBothSides">End margin spread evenly on both sides.</param>
        /// <returns>Position for the center of the object based upon its index in the line.</returns>
        public static float GetCenterPositionByIndex(float maxLength, float objectLength, int index, int count, float marginBothSides = 0f)
        {
            return index
                * GetDistanceFromEachOther(maxLength, objectLength, count, marginBothSides)
                - GetGroupCenterOffset(maxLength, objectLength, marginBothSides);
        }

        /// <summary>
        /// Gets the position of an object in a line of objects.
        /// </summary>
        /// <param name="index">Index of the position in the line.</param>
        /// <param name="distanceFromEachOther">Distance between the objects in a line.</param>
        /// <param name="centerOffSet">Distance to offset the left position of the line to center the line.</param>
        /// <returns></returns>
        public static float GetCenterPositionByIndex(int index, float distanceFromEachOther, float centerOffSet)
        {
            return index * distanceFromEachOther - centerOffSet;
        }

        /// <summary>
        /// Measures the distance between objects in a line of objects taking into account the object's center position.
        /// </summary>
        /// <param name="maxLength">Total length of the line.</param>
        /// <param name="objectLength">Length of a single object in the direction of the line.</param>
        /// <param name="count">Total number of objects in the line.</param>
        /// <param name="distanceBetweenObjects">Margin between objects.</param>
        /// <returns></returns>
        public static float GetDistanceFromEachOther(float maxLength, float objectLength, int count, float distanceBetweenObjects)
        {
            return (maxLength - objectLength - distanceBetweenObjects) / (count - 1);
        }

        /// <summary>
        /// Center point of the total length of a line of objects. Takes into account the object's center position including a margin evenly distributed on both sides.
        /// </summary>
        /// <param name="maxLength">Total available length of the line including the margin.</param>
        /// <param name="objectLength">Length of a single object in the direction of the line.</param>
        /// <param name="distanceBetweenObjects">Margin between objects.</param>
        /// <returns></returns>
        public static float GetGroupCenterOffset(float maxLength, float objectLength, float distanceBetweenObjects)
        {
            return (maxLength - objectLength - distanceBetweenObjects) / 2;
        }

        /// <summary>
        /// Gets height and width of screen/play area size.
        /// </summary>
        /// <param name="camera">Current camera</param>
        /// <returns>Width x Height</returns>
        public static Vector2 GetScreenSize(Camera camera)
        {
            float width = 1 / (camera.WorldToViewportPoint(new Vector3(1, 1, 0)).x - 0.5f);
            float height = 1 / (camera.WorldToViewportPoint(new Vector3(1, 1, 0)).y - 0.5f);

            return new Vector2(width, height);
        }

        /// <summary>
        /// Gets the position at the left edge, vertical center of the play area. Currently does not take into account the size of the object to offset it.
        /// </summary>
        /// <param name="camera">Current camera</param>
        /// <returns></returns>
        public static Vector3 GetScreenLeftCenter(Camera camera)
        {
            return camera.ViewportToWorldPoint(new Vector3(-1, 0.5f, Mathf.Abs(camera.transform.position.z)));
        }

        /// <summary>
        /// Gets the position of the right edge, vertical center of the play area. Currently does not take into account the size of the object to offset it.
        /// </summary>
        /// <param name="camera">Current camera</param>
        /// <returns></returns>
        public static Vector3 GetScreenRightCenter(Camera camera)
        {
            return camera.ViewportToWorldPoint(new Vector3(1, 0.5f, Mathf.Abs(camera.transform.position.z)));
        }

        /// <summary>
        /// Gets the position of the top, horizontal center of the play area. Takes into account the object's size and current depth.
        /// </summary>
        /// <param name="gameObject">Object that is measured and placed at that top center of the screen.</param>
        /// <param name="camera">Current camera</param>
        /// <returns></returns>
        public static Vector3 GetScreenTopCenterPositionForObject(GameObject gameObject, Camera camera)
        {
            var objectSize = GetObjectDimensions(gameObject);

            var top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1, gameObject.transform.position.z + Mathf.Abs(camera.transform.position.z) - (objectSize.z / 2)));
            return new Vector3(0, top.y - objectSize.y / 2, gameObject.transform.position.z);
        }

        /// <summary>
        /// Gets the position of the left, vertical center of the play area. Takes into account the object's size, depth, and destination Z position.
        /// </summary>
        /// <param name="gameObject">Object that is measured and placed at the left center of the screen.</param>
        /// <param name="camera">Current camera</param>
        /// <param name="destinationZ">Destination Z position</param>
        /// <returns></returns>
        public static Vector3 GetScreenLeftCenterPositionForObject(Vector3 objectDimensions, Camera camera, float destinationZ)
        {
            // keep in mind that the face closest to the camera is half the depth closer than the center of the object)
            var leftMiddleWithDepth = camera.ViewportToWorldPoint(new Vector3(0, 0.5f, Mathf.Abs(camera.transform.position.z) + destinationZ - (objectDimensions.z / 2)));
            return new Vector3(leftMiddleWithDepth.x + objectDimensions.x / 2, leftMiddleWithDepth.y, destinationZ);
        }

        /// <summary>
        /// Gets the position of the bottom, horizontal center of the play area. Takes into account the object's size, depth, and destination Z position.
        /// </summary>
        /// <param name="gameObject">Object that is measured and placed at the bottom center of the screen.</param>
        /// <param name="camera">Current camera</param>
        /// <param name="destinationZ">Destination Z position</param>
        /// <returns></returns>
        public static Vector3 GetScreenBottomCenterPositionForObject(GameObject gameObject, Camera camera, float destinationZ)
        {
            var objectSize = GetObjectDimensions(gameObject);

            var leftMiddleWithDepth = camera.ViewportToWorldPoint(new Vector3(0.5f, 0, Mathf.Abs(camera.transform.position.z) + destinationZ - (objectSize.z / 2)));
            return new Vector3(0, leftMiddleWithDepth.y + objectSize.y / 2, destinationZ);
        }

        /// <summary>
        /// Gets the size of the object's bounding box.
        /// </summary>
        /// <param name="gameObject">Game object to measure</param>
        /// <returns>Object's width, height, and depth</returns>
        public static Vector3 GetObjectDimensions(GameObject gameObject)
        {
            var renderer = gameObject.transform.GetComponent<Renderer>();   // all objects must be the same shape/size
            return renderer.bounds.size;
        }

        /// <summary>
        /// Spreads a list of objects across the width of the screen at the yPos position. Ideally replaced by GetPositionsSpreadAcrossLength().
        /// </summary>
        /// <param name="objects">Objects that will be spread across the screen.</param>
        /// <param name="camera">Current camera</param>
        /// <param name="yPos">Destination Y position</param>
        /// <param name="sideMargin">Margin spread evenly across both sides of the line of objects</param>
        public static void LayoutAcrossScreen(List<GameObject> objects, Camera camera, float yPos, float sideMargin) // TODO: margin is distance from sides of screen. Ideally should be percentages for smaller screens? or some cutoff at some point?
        {
            Vector3 objectSize = GetObjectDimensions(objects[0]);
            float objectWidth = objectSize.x;
            Vector2 screenSize = GetScreenSize(camera);
            List<float> positions = GetPositionsSpreadAcrossLength(screenSize.x, objectWidth, objects.Count, sideMargin);

            for (int i = 0; i < objects.Count; i++)
            {
                objects[i].transform.position = new Vector3(positions[i], yPos, 0);
            }
        }

        public static List<Vector3> GetLayoutAcrossScreen(Vector3 objectDimensions, Camera camera, int totalCount, float sideMargin)
        {
            Vector2 screenSize = GetScreenSize(camera);
            List<float> xPositions = GetPositionsSpreadAcrossLength(screenSize.x, objectDimensions.x, totalCount, sideMargin);

            var positions = new List<Vector3>();
            foreach (var x in xPositions)
            {
                positions.Add(new Vector3(x, GetPlayerDominoYPosition(objectDimensions, camera), 0));
            }

            return positions;
        }

        public static float GetPlayerDominoYPosition(Vector3 objectDimensions, Camera camera)
        {
            Vector3 screenBottom = camera.ViewportToWorldPoint(new Vector3(0, 0, Mathf.Abs(camera.transform.position.z)));

            return screenBottom.y + objectDimensions.y / 2;
        }

        // TODO: begin using GetScreenBottomCenterPositionForObject() and provide a Y position
        public static void LayoutAcrossAndUnderScreen(List<GameObject> objects, Camera camera, float sideMargin)
        {
            Vector3 objectSize = GetObjectDimensions(objects[0]);
            float objectWidth = objectSize.x;
            Vector2 screenSize = GetScreenSize(camera);
            List<float> positions = GetPositionsSpreadAcrossLength(screenSize.x, objectWidth, objects.Count, sideMargin);
            Vector3 screenBottom = camera.ViewportToWorldPoint(new Vector3(0, 0, Mathf.Abs(camera.transform.position.z)));

            for (int i = 0; i < objects.Count; i++)
            {
                objects[i].transform.position = new Vector3(positions[i], screenBottom.y - objectSize.y, 0);
            }
        }

        public static List<float> SpreadByMargin(float objectLength, int objectCount, float margin)
        {
            var positions = new List<float>();
            float totalWidth = ((objectCount - 1) * margin) + (objectCount * objectLength);
            float centerOffset = totalWidth / 2;
            float evenOffset = objectCount % 2 == 0 ? (objectLength / 2) + (margin / 2) : 0;

            for (int i = 0; i < objectCount; i++)
            {
                float fromLeft = i * (objectLength + margin);
                float position = fromLeft - centerOffset + evenOffset;
                positions.Add(position);
            }

            return positions;
        }

        /// <summary>
        /// Spreads objects across a given length.
        /// </summary>
        /// <param name="maxLength">Length to spread the objects across</param>
        /// <param name="objectLength">Length of one of the objects that will be spread across the line</param>
        /// <param name="objectCount">Number of objects to spread across the line length</param>
        /// <param name="endMargins">Margin evenly spread across both sides of the line length</param>
        /// <returns>List of destination positions</returns>
        public static List<float> GetPositionsSpreadAcrossLength(float maxLength, float objectLength, int objectCount, float endMargins)
        {
            List<float> positions = new List<float>();
            var distanceFromEachOther = GetDistanceFromEachOther(maxLength, objectLength, objectCount, endMargins);
            float centerOffSet = GetGroupCenterOffset(maxLength, objectLength, endMargins);
            for (int i = 0; i < objectCount; i++)
            {
                positions.Add(GetCenterPositionByIndex(i, distanceFromEachOther, centerOffSet));
            }
            return positions;
        }
    }
}
