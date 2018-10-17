using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysHelper {
    ///<summary>Do a fake cylinder cast made out of multiple box casts (vertical cylinders supported only for now)</summary>
    static public bool FakeCylinderCast(int numBoxes, float height, float radius, Vector3 center, Vector3 direction, out RaycastHit hitinfo, float distance, int layerMask, bool drawDebug = false)
    {
        bool collided = false;
        hitinfo = new RaycastHit();
        float rotateAngle = 90.0f / (numBoxes);
        float rotateAngleRad = rotateAngle * Mathf.Deg2Rad;
        Vector3 halfExtents = new Vector3(Mathf.Cos(rotateAngleRad) * radius, height / 2, Mathf.Sin(rotateAngleRad) * radius);
        RaycastHit currentHitInfo;
        float minDistance = distance;
        for (int i = 0; i < numBoxes; i++)
        {
            Quaternion newOrientation = Quaternion.Euler(0, rotateAngle * i * 2, 0);
            if (Physics.BoxCast(center, halfExtents, direction, out currentHitInfo, newOrientation, distance, layerMask))
            {
                if (drawDebug)
                    DrawBoxCast(halfExtents, center, center + currentHitInfo.distance * direction, newOrientation, true);
                if (currentHitInfo.distance < minDistance)
                {
                    collided = true;
                    minDistance = currentHitInfo.distance;
                    hitinfo = currentHitInfo;
                }
            }
            else if (drawDebug)
                DrawBoxCast(halfExtents, center, center + distance * direction, newOrientation, false);
        }
        return collided;
    }
    ///<summary>Draw a box</summary>
    static public void DrawBox(Vector3 halfExtents, Vector3 center, Quaternion orientation, Color color)
    {
        Vector3[] boxPoints = new Vector3[8];
        int currentIndex = 0;
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    boxPoints[currentIndex] = Vector3.Scale(halfExtents, new Vector3(i, j, k));
                    boxPoints[currentIndex] = orientation * boxPoints[currentIndex];
                    boxPoints[currentIndex] += center;
                    currentIndex++;
                }
            }
        }
        // x connections
        Debug.DrawLine(boxPoints[0], boxPoints[4], color);
        Debug.DrawLine(boxPoints[1], boxPoints[5], color);
        Debug.DrawLine(boxPoints[2], boxPoints[6], color);
        Debug.DrawLine(boxPoints[3], boxPoints[7], color);
        // y connections
        Debug.DrawLine(boxPoints[0], boxPoints[2], color);
        Debug.DrawLine(boxPoints[1], boxPoints[3], color);
        Debug.DrawLine(boxPoints[4], boxPoints[6], color);
        Debug.DrawLine(boxPoints[5], boxPoints[7], color);
        // z connections
        Debug.DrawLine(boxPoints[0], boxPoints[1], color);
        Debug.DrawLine(boxPoints[2], boxPoints[3], color);
        Debug.DrawLine(boxPoints[4], boxPoints[5], color);
        Debug.DrawLine(boxPoints[6], boxPoints[7], color);
    }
    
    ///<summary>Draw a box (default color: white)</summary>
    static public void DrawBox(Vector3 halfExtents, Vector3 center, Quaternion orientation)
    {
        DrawBox(halfExtents, center, orientation, Color.white);
    }
    
    ///<summary>Draw a box cast. Center1 is the original box and center2 is where the box is when the cast ended. If didCollide is true you'll get a red end box, if false you'll get a green one.
    static public void DrawBoxCast(Vector3 halfExtents, Vector3 center1, Vector3 center2, Quaternion orientation, bool didCollide)
    {
        DrawBox(halfExtents, center1, orientation);
        DrawBox(halfExtents, center2, orientation, didCollide ? Color.red : Color.green);
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 currentPoint = Vector3.Scale(halfExtents, new Vector3(i, j, k));
                    currentPoint = orientation * currentPoint;
                    Debug.DrawLine(currentPoint + center1, currentPoint + center2);
                }
            }
        }
    }
}
