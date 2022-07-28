using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestClayxelSolids : MonoBehaviour
{
    public ParticleSystem particleSys;
    public int solidCount = 100;
    public Color color;

    Clayxels.ClayContainer clayxel;
    ParticleSystem.Particle[] particles;
    float rot = 0.0f;
    List<Clayxels.Solid> solids;
    int preAddedSolids = 0;
    int oldSolidCount = 0;
	
    void Start()
    {
        this.clayxel = this.gameObject.GetComponent<Clayxels.ClayContainer>();
        this.clayxel.init();

        this.solids = this.clayxel.getSolids();
        this.preAddedSolids = this.solids.Count; // remember if user added solids before particles
        
        this.updateParticleCount();
    }

    void updateParticleCount(){
        if(this.solidCount > 100){
            this.solidCount = 100;
        }
        else if(this.solidCount < 10){
            this.solidCount = 10;
        }

        this.oldSolidCount = this.solidCount;

        this.solids = this.clayxel.getSolids();
        this.particles = new ParticleSystem.Particle[this.solidCount];

        if(this.solids.Count > this.preAddedSolids + 1){
            this.solids.RemoveRange(this.preAddedSolids, this.solids.Count - 1);
        }

        int count = this.solidCount;
        if(count > this.particleSys.particleCount){
            count = this.particleSys.particleCount;
        }

        for(int i = 0; i < count; ++i){
            Clayxels.Solid newSolid = new Clayxels.Solid();
            
            newSolid.primitiveType = 0;
            newSolid.attrs.x = 0.5f;
            newSolid.blend = 0.5f;

            float randomDarkening = UnityEngine.Random.Range(0.5f, 1.0f);
            newSolid.color.x = this.color.r * randomDarkening;
            newSolid.color.y = this.color.g * randomDarkening;
            newSolid.color.z = this.color.b * randomDarkening;
            
            this.solids.Add(newSolid);
        }

        this.clayxel.updatedSolidCount();
    }

    void FixedUpdate(){
        if(this.solidCount != this.oldSolidCount){// detect solidCount change
            this.updateParticleCount();
        }

        this.particleSys.GetParticles(this.particles);
		
        for(int i = 0; i < this.solids.Count - this.preAddedSolids; ++i){
             if(i > this.particleSys.particleCount){
                return;
            }

        	Clayxels.Solid solid = this.solids[i + this.preAddedSolids];

        	solid.position = this.particles[i].position - this.transform.position;
            solid.scale = this.particles[i].GetCurrentSize3D(this.particleSys);

            solid.rotation = Quaternion.Euler(10.0f * i, 10.0f * i, this.rot * i);
            
            this.clayxel.solidUpdated(solid.id);
        }

        this.rot += 0.1f;
    }
}
