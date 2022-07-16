using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VMBackuperBeckEnd.Models;
using VMBackuper.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Renci.SshNet;
using VMBackuper.Services;
using AutoMapper;
using VMBackuper.Models.DTOs;

namespace VMBackuper.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    //[Authorize]
    public class HyperVisorItemsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<UserAccount> _userManager;
        private readonly IWebHostEnvironment _hostEnv;

        public HyperVisorItemsController(AppDbContext context, UserManager<UserAccount> userManager, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _hostEnv = hostingEnvironment;
        }

        // GET: api/HyperVisorItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HyperVisorItem>>> GetHyperVisors()
        {
            //var user = User.Identity.Name; //await _userManager.GetUserAsync(User);
            var user = await _userManager.GetUserAsync(User);
            return await _context.HyperVisors.ToListAsync();
        }

        // GET: api/HyperVisorItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<HyperVisorItem>> GetHyperVisorItem(int id)
        {
            var hyperVisorItem = await _context.HyperVisors.FindAsync(id);

            if (hyperVisorItem == null)
            {
                return NotFound();
            }

            return hyperVisorItem;
        }

        // PUT: api/HyperVisorItems/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHyperVisorItem(int id, HyperVisorItem hyperVisorItem)
        {
            if (id != hyperVisorItem.Id)
            {
                return BadRequest();
            }

            var hvItem = await _context.HyperVisors.FindAsync(id);
            hvItem.Name = hyperVisorItem.Name;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HyperVisorItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/HyperVisorItems
        [HttpPost("VMSList")]
        public async Task<ActionResult<HyperVisorItem>> GetVMsList(HyperVisorItem hyperVisorItem)
        {
            var hitem = await _context.HyperVisors.FindAsync(hyperVisorItem.Id);
            if (hitem != null)
            {
                SshClient ssh = HyperSshService.GetSshClientInstanceBySavedCredentials(hitem);
                return Ok(HyperSshService.GetVmList(ssh));
            }

            return Ok(new List<VirtualMachine>());
        }

        // POST: api/HyperVisorItems
        [HttpPost("RecoveryList")]
        public async Task<ActionResult<HyperVisorItem>> GetRecoveryList(HyperVisorItem hyperVisorItem)
        {
            var hitem = await _context.HyperVisors.FindAsync(hyperVisorItem.Id);
            if (hitem != null)
            {
                return Ok(HyperSshService.GetRecoveryList(hitem));
            }
            return Ok(new Folder());
        }

        // POST: api/HyperVisorItems
        [HttpPost("SaveGhettoConfig")]
        public async Task<ActionResult<HyperVisorItem>> SaveConfig(SaveGhettoConfigDTO data)
        {
            var hitem = await _context.HyperVisors.FindAsync(data.HyperVisor.Id);
            if (hitem != null)
            {
                HyperSshService.SaveGhettoConfig(hitem, data.Config);
                return Ok();
            }
            return Ok();
        }

        // POST: api/HyperVisorItems
        [HttpPost("DeleteImage")]
        public async Task<ActionResult<HyperVisorItem>> DeleteImage(RecoveryImageDTO data)
        {
            var hitem = await _context.HyperVisors.FindAsync(data.HyperVisor.Id);
            if (hitem != null)
            {
                var isDeleted = HyperSshService.DeleteImage(hitem, data.Folder);
                return isDeleted ? Ok() : NoContent();
            }
            return NoContent();
        }

        // POST: api/HyperVisorItems
        [HttpPost("RestoreImage")]
        public async Task<ActionResult<HyperVisorItem>> RestoreImage(RestoreDataDTO data)
        {
            Debug.WriteLine("RestoreImage");
            var hitem = await _context.HyperVisors.FindAsync(data.HyperVisor.Id);
            if (hitem != null)
            {
                HyperShell hs = new();
                hs = HyperSshService.RestoreImage(hitem, data.RestoreConfig);
                return hs.Success ? Ok(hs) : NoContent();
            }
            return NoContent();
        }

        // POST: api/HyperVisorItems
        [HttpPost("DestroyVM")]
        public async Task<ActionResult<HyperVisorItem>> DestroyVM(VMDataDTO data)
        {
            Debug.WriteLine("DestroyVM");
            var hitem = await _context.HyperVisors.FindAsync(data.HyperVisor.Id);
            if (hitem != null)
            {
                HyperShell hs = new();
                hs = HyperSshService.DestroyVM(hitem, data.VirtualMachine);
                return hs.Success ? Ok(hs) : NoContent();
            }
            return NoContent();
        }

        // POST: api/HyperVisorItems
        [HttpPost("GetGhettoConfig")]
        public async Task<ActionResult<HyperVisorItem>> GetConfig(HyperVisorItem hyperVisorItem)
        {
            var hitem = await _context.HyperVisors.FindAsync(hyperVisorItem.Id);
            if (hitem != null)
            {
                return Ok(HyperSshService.GetGhettoConfig(hitem));
            }

            return Ok();
        }

        //[Route("VMBackup")]
        [HttpPost("VMBackup")]
        public async Task<ActionResult<HyperVisorItem>> VMBackup(VMDataDTO vm)
        {
            var hitem = await _context.HyperVisors.FindAsync(vm.HyperVisor.Id);
            if (hitem != null)
            {
                HyperShell hyperShell = new();
                hyperShell = HyperSshService.BackupVM(hitem, vm.VirtualMachine);
                return Ok(hyperShell);
            }

            return BadRequest();
        }

        // POST: api/HyperVisorItems
        [HttpPost]
        public async Task<ActionResult<HyperVisorItem>> PostHyperVisorItem(HyperVisorItemAddDTO hyperVisorItem)
        {
            TimeZoneInfo timeZoneInfo = TimeZoneInfo.Local;
            DateTime dateTime;

            // for windows
            try { timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); } catch (TimeZoneNotFoundException) { }
            // for linux
            try { timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Tallinn"); } catch (TimeZoneNotFoundException) { }

            dateTime = TimeZoneInfo.ConvertTime(DateTime.Now, timeZoneInfo);

            // map DTO to HV
            var config = new MapperConfiguration(cfg => cfg.CreateMap<HyperVisorItemAddDTO, HyperVisorItem>());
            var mapper = new Mapper(config);
            var _hyperV = mapper.Map<HyperVisorItem>(hyperVisorItem);

            _hyperV.DateAdded = dateTime;

            // Connect to server and generate RSA keys
            // on vmware keygen location: /usr/lib/vmware/openssh/bin/ssh-keygen
            // VMWare server needed:(publickey,keyboard-interactive) SSH login
            // With default has error: "No suitable authentication method found to complete authentication (publickey,keyboard-interactive)"
            // need to detect possible connection type...
            var ssh = new HyperSshService(_hyperV, hyperVisorItem.Password);
            var connection = ssh.InitializeSshConnection();

            try
            {
                if (connection.Success)
                {
                    // generate new SSH Private key
                    Random random = new Random();
                    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                    string passPhrase = new string(Enumerable.Repeat(chars, 32).Select(s => s[random.Next(s.Length)]).ToArray());
                    string backupConfFileName = new string(Enumerable.Repeat(chars, 16).Select(s => s[random.Next(s.Length)]).ToArray());
                    var isGeneratedRsaKeys = ssh.GenerateRSAKeys(passPhrase);
                    Debug.WriteLine($"isGeneratedRsaKeys: {isGeneratedRsaKeys}");
                    if (isGeneratedRsaKeys)
                    {
                        // Register generated keys on the remote server
                        var isRegisteredKeys = ssh.RegisterAuthKeysOnServer();
                        if (isRegisteredKeys)
                        {
                            // store to db passphrase for keyfile
                            _hyperV.PrivateKeyPassPhrase = passPhrase;
                            // save content of keys to backend
                            string uniqueString = Guid.NewGuid().ToString();
                            string PrivateKeyFileName = uniqueString + ".key";
                            string PublicKeyFileName = PrivateKeyFileName + ".pub";
                            _hyperV.PrivateKeyFileName = PrivateKeyFileName;
                            _hyperV.PublicKeyFileName = PublicKeyFileName;
                            _hyperV.GhettoBackupConfigFileName = "VMBackup_" + backupConfFileName + ".conf";
                            string PrivateKeyFileNamePath = Path.Combine(HyperSshService.PrivateKeysDirectory, PrivateKeyFileName);
                            string PublicKeyFileNamePath = Path.Combine(HyperSshService.PrivateKeysDirectory, PublicKeyFileName);
                            // save private key
                            await System.IO.File.WriteAllTextAsync(PrivateKeyFileNamePath, ssh.PrivateKeyContent);
                            // save public key
                            await System.IO.File.WriteAllTextAsync(PublicKeyFileNamePath, ssh.PublicKeyContent);

                            ////------------------------------------------------------------------------------------
                            //// try to connect via keys
                            var sshClient = HyperSshService.GetSshClientInstanceBySavedCredentials(_hyperV);
                            {
                                sshClient.Connect();
                                SshCommand cli = sshClient.CreateCommand($"echo {passPhrase}");
                                cli.Execute();
                                if (!cli.Result.Contains(passPhrase))
                                    throw new Exception("Error with Private Key Connection!");
                                sshClient.Disconnect();
                            }
                        }
                    }
                }
                else
                {
                    return BadRequest(new HyperVisorResult()
                    {
                        Errors = new List<string>() { "SSH Connection initialize error.", "Check the remote host, port, username, password." },
                        Success = false
                    });
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error: {e.Message}");
                return BadRequest(new HyperVisorResult()
                {
                    Errors = new List<string>() { e.Message },
                    Success = false
                });
            }
            _context.HyperVisors.Add(_hyperV);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetHyperVisorItem", new { id = _hyperV.Id }, new HyperVisorItemDTO()
            {
                Name = _hyperV.Name,
                UserName = _hyperV.UserName,
                Port = _hyperV.Port,
                HostName = _hyperV.HostName
            });
        }

        // DELETE: api/HyperVisorItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHyperVisorItem(int id)
        {
            var hyperVisorItem = await _context.HyperVisors.FindAsync(id);
            if (hyperVisorItem == null)
            {
                return NotFound();
            }
            //--------------------------------------------------------------
            // before delete hyperItem from db, will try to remove public keys from remote mashine if connection OK
            try { HyperSshService.RemovePublicKeysFromServerAuth(hyperVisorItem); }
            catch (Exception) { }
            //--------------------------------------------------------------
            _context.HyperVisors.Remove(hyperVisorItem);
            try
            {
                // remove private keys relations of deleted visor
                System.IO.File.Delete(Path.Combine(HyperSshService.PrivateKeysDirectory, hyperVisorItem.PrivateKeyFileName));
                System.IO.File.Delete(Path.Combine(HyperSshService.PrivateKeysDirectory, hyperVisorItem.PublicKeyFileName));
            }
            catch (Exception) { }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool HyperVisorItemExists(int id)
        {
            return _context.HyperVisors.Any(e => e.Id == id);
        }
    }
}
