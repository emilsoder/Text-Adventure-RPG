﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Engine;
using System.Xml;
using System.IO;

namespace RPGAdventurePlus
{
    public partial class frm_GamePlay : Form
    {
        public Player _player;
        private Monster _currentMonster;
        private bool InCombat = false;
        private string saveFileName;

        public frm_GamePlay(Player newPlayer, string SaveFileName)
        {
            InitializeComponent();
            saveFileName = SaveFileName;
           
            _player = newPlayer;
            // UI databindning
            #region UI DATABINDNINGAR
            #region fonter
            lblCurrentMana.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            lblExperience.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            lblLevel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            #endregion fonter
            lblHitPoints.DataBindings.Add("Text", _player, "CurrentHitPoints");
            lblGold.DataBindings.Add("Text", _player, "Gold");
            lblExperience.DataBindings.Add("Text", _player, "ExperiencePoints");
            lblLevel.DataBindings.Add("Text", _player, "Level");
            lblCurrentMana.DataBindings.Add("Text", _player, "CurrentMana");

            dgvInventory.RowHeadersVisible = false;
            dgvInventory.AutoGenerateColumns = false;
            dgvInventory.DataSource = _player.Inventory;

            dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                Width = 197,
                DataPropertyName = "Description"
            });

            dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Quantity",
                DataPropertyName = "Quantity"
            });

            dgvQuests.RowHeadersVisible = false;
            dgvQuests.AutoGenerateColumns = false;
            dgvQuests.DataSource = _player.Quests;

            dgvQuests.Columns.Add(new DataGridViewTextBoxColumn{
                HeaderText = "Name",
                Width = 197,
                DataPropertyName = "Name"
            });

            dgvQuests.Columns.Add(new DataGridViewTextBoxColumn{
                HeaderText ="Done?",
                DataPropertyName="IsCompleted"
            });

            cbo_Weapons.DataSource = _player.Weapons;
            cbo_Weapons.DisplayMember = "Name";
            cbo_Weapons.ValueMember = "ID";

            if (_player.CurrentWeapon != null)
            {
                cbo_Weapons.SelectedItem = _player.CurrentWeapon;
            }

            cbo_Weapons.SelectedIndexChanged += cboWeapons_SelectedIndexChanged;

            cbo_Potions.DataSource = _player.Potions;
            cbo_Potions.DisplayMember = "Name";
            cbo_Potions.ValueMember = "ID";

            if (_player.CurrentPotion != null)
            {
                cbo_Potions.SelectedItem = _player.CurrentPotion;
            }

            cbo_Potions.SelectedIndexChanged += cboPotions_SelectedIndexChanged;

            cbo_Spells.DataSource = _player.Spells.Select(x => x.Details).ToList();
            cbo_Spells.DisplayMember = "Name";
            cbo_Spells.ValueMember = "ID";

            _player.PropertyChanged += PlayerOnPropertyChanged;

            #endregion DATABINDNINGAR
            UpdateUI();
            MoveTo(_player.CurrentLocation);
        }

        private void PlayerOnPropertyChanged(Object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == "Weapons")
            {
                cbo_Weapons.DataSource = _player.Weapons;
            }

            if (propertyChangedEventArgs.PropertyName == "Potions")
            {
                cbo_Potions.DataSource = _player.Potions;
                if (!_player.Potions.Any() || InCombat == false)
                {
                    cbo_Potions.Visible = false;
                    btnUsePotion.Visible = false;
                }
            }

            if (propertyChangedEventArgs.PropertyName == "Spells")
            {
                cbo_Spells.DataSource = _player.Spells.Select(x => x.Details).ToList();
            }
        }
        
        // Förflytta spelaren till en ny plats
        private void MoveTo(Location newLocation)
        {
            bool AllowPlayerToMove = true;
            if (newLocation.HasRequirementToEnter() && !_player.HasItem(newLocation.ItemRequiredToEnter, 1))
            {
                ShowMessage("Du behöver " + newLocation.ItemRequiredToEnter.Name + " för att gå hit.");
                AllowPlayerToMove = false;
            }
            if(newLocation.LevelRequirement >_player.Level)
            {
                ShowMessage("Du måste minst vara på level " + newLocation.LevelRequirement.ToString() + " för att gå hit.");
                AllowPlayerToMove = false;
            }
            if(AllowPlayerToMove == true)
            {
                _player.CurrentLocation = newLocation;
                _player.Heal(_player.MaximumHitPoints);
                _player.RestoreManaToFull();
               
                InCombat = false;

                #region OM DET FINNS ETT UPPDRAG VID AKTUELL PLATS
                // if there's a quest available here
                if (newLocation.HasQuestAvailable()) 
                {
                    if (!_player.HasOngoingQuest(newLocation.QuestAvailableHere) && !_player.IsQuestCompleted(newLocation.QuestAvailableHere))
                    {
                        StartQuest(newLocation.QuestAvailableHere);
                    }
                    else if(!_player.IsQuestCompleted(newLocation.QuestAvailableHere))
                    {
                        bool HaveQuestItems = true;

                        #region KOLLA OM SPELAREN HAR ALLA KRAV (ITEMS)
                        // check if the player has all the required items
                        foreach (QuestCompletionItem qci in newLocation.QuestAvailableHere.QuestCompletionItems)
                        {
                            if (!_player.HasItem(qci.Details, qci.Quantity))
                            {
                                HaveQuestItems = false;
                            }
                        }

                        if (HaveQuestItems == true) 
                        {
                            foreach (QuestCompletionItem qci in newLocation.QuestAvailableHere.QuestCompletionItems)
                            {
                                _player.RemoveItem(GetInventoryItem(qci.Details), qci.Quantity);
                            }
                            CompleteQuest(GetPlayerQuest(newLocation.QuestAvailableHere));
                        }
                        #endregion
                    }                        
                }
                #endregion

                #region OM PLATSEN INNEHÅLLER ETT MONSTER, STARTA EN KAMP
                if (newLocation.HasLivingMonster())
                {
                    BeginBattle(newLocation.MonsterLivingHere);
                }
                #endregion
            }
            UpdateUI();
        }

        private void ShowMessage(string message)
        {
            rtbMessages.Text += message + Environment.NewLine;
            rtbMessages.Text += Environment.NewLine;
            rtbMessages.SelectionStart = rtbMessages.Text.Length;
            rtbMessages.ScrollToCaret();
        }
        public void UpdateUI()
        {
            btnNorth.Visible = (_player.CurrentLocation.LocationToNorth != null);
            btnEast.Visible = (_player.CurrentLocation.LocationToEast != null);
            btnSouth.Visible = (_player.CurrentLocation.LocationToSouth != null);
            btnWest.Visible = (_player.CurrentLocation.LocationToWest != null);
            rtbLocation.Text = _player.CurrentLocation.Name + Environment.NewLine;
            rtbLocation.Text += _player.CurrentLocation.Description + Environment.NewLine;

            lblHitPoints.Font = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size, FontStyle.Bold);

            rtbLocation.Text = _player.CurrentLocation.Name + Environment.NewLine;
            rtbLocation.Text += _player.CurrentLocation.Description + Environment.NewLine;

            if (!_player.Weapons.Any() || InCombat == false)
            {
                cbo_Weapons.Visible = false;
                btnUseWeapon.Visible = false;
            }
            else
            {
                cbo_Weapons.Visible = true;
                btnUseWeapon.Visible = true;
            }

            if (!_player.Spells.Any() || InCombat == false)
            {
                cbo_Spells.Visible = false;
                btnUseSpell.Visible = false;
            }
            else
            {
                cbo_Spells.Visible = true;
                btnUseSpell.Visible = true;
            }
            if (!_player.Potions.Any() || InCombat == false)
            {
                cbo_Potions.Visible = false;
                btnUsePotion.Visible = false;
            }
            else
            {
                cbo_Potions.Visible = true;
                btnUsePotion.Visible = true;
            }
        }
      
        private void RewardXP (int amount)
        {
            _player.ExperiencePoints += amount;
            if ((_player.ExperiencePoints / 10) >= _player.Level)
            {
                _player.ExperiencePoints -= _player.Level * 10;
                _player.LevelUp();
                ShowMessage("Gratulerar! Du har levlat upp!");
                ShowMessage("Du är nu på level " + _player.Level.ToString());
            }
        }
        private void CompleteQuest (PlayerQuest quest)
        {
            ShowMessage("Uppdraget avklarat! Du får:");
            ShowMessage(quest.Details.RewardExperiencePoints.ToString() + " EXP");
            ShowMessage(quest.Details.RewardGold.ToString() + " Guldpengar");
            if (quest.Details.RewardItem != null)
            {
                ShowMessage(quest.Details.RewardItem.Name);
                _player.AddItem(quest.Details.RewardItem, 1);
            }
            RewardXP(quest.Details.RewardExperiencePoints);
            _player.RewardGold(quest.Details.RewardGold);
            quest.IsCompleted = true;
        }
        private PlayerQuest GetPlayerQuest (Quest quest)
        {
            foreach(PlayerQuest playerQuest in _player.Quests)
            {
                if (playerQuest.Details == quest)
                    return playerQuest;
            }
            return null;
        }
        private InventoryItem GetInventoryItem(Item item)
        {
            foreach(InventoryItem inventoryItem in _player.Inventory)
            {
                if (inventoryItem.Details == item)
                    return inventoryItem;
            }
            return null;
        }

        #region NOT IN USE
        private void ShowCombatUI()
        {
            if (cbo_Weapons.SelectedText != null)
            {

            }
            cbo_Weapons.Visible = true;
            cbo_Potions.Visible = true;
            btnUsePotion.Visible = true;
            btnUseWeapon.Visible = true;
        }
        private void HideCombatUI()
        {
            cbo_Weapons.Visible = false;
            cbo_Potions.Visible = false;
            btnUsePotion.Visible = false;
            btnUseWeapon.Visible = false;
        }
        private QuestCompletionItem GetQuestCompletionItem (Quest quest)
        {
            foreach(QuestCompletionItem qci in quest.QuestCompletionItems)
            {
                if(qci.Details.ID == quest.ID)
                {
                    return qci;
                }
            }
            return null;
        }
        #endregion

        private void StartQuest(Quest quest)
        {
            ShowMessage("");
            ShowMessage("");
            ShowMessage("Uppdrag påbörjat! " + quest.Name);
            ShowMessage(quest.Description);
            ShowMessage("Du måste ha: ");
            foreach(QuestCompletionItem qci in quest.QuestCompletionItems)
            {
                if(qci.Quantity == 1)
                {
                    ShowMessage(qci.Quantity.ToString() + " " + qci.Details.Name);
                }
                else
                {
                    ShowMessage(qci.Quantity.ToString() + " " + qci.Details.NamePlural);
                }
            }
            ShowMessage("");
            _player.Quests.Add(new PlayerQuest(quest));
        }
       
        private void BeginBattle(Monster monster, bool boss = false)
        {
            ShowMessage("Du ser " + monster.Name);
            Monster StandardMonster = World.MonsterByID(monster.ID);
            _currentMonster = new Monster(StandardMonster.ID, StandardMonster.Name, StandardMonster.MaximumDamage, StandardMonster.RewardExperiencePoints, StandardMonster.RewardGold, StandardMonster.CurrentHitPoints, StandardMonster.MaximumHitPoints, StandardMonster.Level,StandardMonster.Strength,StandardMonster.Dexterity,StandardMonster.Intelligent,StandardMonster.CurrentMana,StandardMonster.MaximumMana,StandardMonster.CreatureRace,StandardMonster.ArmourUsed);
            _currentMonster.Spells = StandardMonster.Spells;

            if (boss == true)
            {
                _currentMonster.MaximumDamage *= 2;
                _currentMonster.MaximumHitPoints *= 2;
                _currentMonster.CurrentHitPoints = _currentMonster.MaximumHitPoints;
            }
            foreach (LootItem lootItem in StandardMonster.LootTable)
            {
                _currentMonster.LootTable.Add(lootItem);
            }
            InCombat = true;
            UpdateUI();
        }

        private void btnNorth_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToNorth);
        }

        private void btnWest_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToWest);
        }

        private void btnEast_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToEast);
        }

        private void btnSouth_Click(object sender, EventArgs e)
        {
            MoveTo(_player.CurrentLocation.LocationToSouth);
        }

        private void btnCompleteQuest_Click(object sender, EventArgs e)
        {
            _player.AddItem(World.ItemByID(World.ITEM_ID_SNAKE_FANG), 10);
            _player.AddItem(World.ItemByID(World.ITEM_ID_HEALING_POTION), 3);
            _player.AddItem((Item)World.ItemByID(World.ITEM_ID_LEGENDARY_SWORD),1);
            _player.AddItem(World.ItemByID(World.ITEM_ID_DEATHCLAW_HAND), 1);
            _player.AddSpell(World.SpellByID(World.SPELL_ID_FIREBALL));
            _player.AddItem(World.ItemByID(World.ITEM_ID_ADVENTURER_PASS),1);
            _player.AddSpell(World.SpellByID(World.SPELL_ID_RESTORE_HEALTH));
            _player.AddSpell(World.SpellByID(World.SPELL_ID_RESTORE_MANA));
            _player.AddSpell(World.SpellByID(World.SPELL_ID_ENDURANCE));
            _player.MaximumMana = 100;
            _player.RestoreManaToFull();
        }
        private void EndCombat(Monster monster)
        {
            // End combat, reward XP, gold and items if any
            InCombat = false;
            
            ShowMessage("You received: " + monster.RewardExperiencePoints.ToString() + " EXP");
            _player.RewardGold(monster.RewardGold);
            ShowMessage("You received: " + monster.RewardGold.ToString() + " gold!");
            List<InventoryItem> lootedItems = new List<InventoryItem>();
            foreach(LootItem lootItem in _currentMonster.LootTable)
            {
                if(RandomNumber.Between(1,100) <= lootItem.DropPercentage)
                {
                    lootedItems.Add(new InventoryItem(lootItem.Details, 1));
                }
            }
            if(lootedItems.Count != 0)
            {
                foreach(InventoryItem inventoryItem in lootedItems)
                {
                    _player.AddItem(inventoryItem.Details, inventoryItem.Quantity);
                    ShowMessage("You received: " + inventoryItem.Quantity.ToString() + " " + inventoryItem.Details.Name);
                }
            }
            RewardXP(monster.RewardExperiencePoints);
            _player.RemoveAllBuffs();
            UpdateUI();
        }
        private void CombatPhase (int Action) 
        {
            if (InCombat == true)
            {
                int damageDealt;
                if (Action == 1) // attackera
                {
                    if (RandomNumber.Between(_player.Level, _player.Level + 5) - (_currentMonster.Level) <= 0)
                    {
                        ShowMessage("Du missade din möjlighet till attack!");
                    }
                    else
                    {
                        Weapon currentWeapon = (Weapon)cbo_Weapons.SelectedItem;
                        damageDealt = RandomNumber.Between(currentWeapon.MinimumDamage, currentWeapon.MaximumDamage);
                        damageDealt += _player.Level - _currentMonster.Level;

                        if (_currentMonster.ArmourUsed != null)
                        {
                            damageDealt -= _currentMonster.ArmourUsed.Resistance / 2;
                        }
                        if (damageDealt <= 0) 
                        {
                            ShowMessage("Din attack gjorde ingen skada!");
                        }
                        else
                        {
                            _currentMonster.DealDamage(damageDealt);
                            ShowMessage("Du gav " + damageDealt.ToString() + " poäng skada på " + _currentMonster.Name);
                        }
                    }
                }
                else if(Action == 2) // använd hälsodryck
                {
                    HealingPotion potion = (HealingPotion)cbo_Potions.SelectedItem;
                    _player.Heal(potion.AmountToHeal);
                    _player.RemoveItem(GetInventoryItem((Item)potion), 1);
                    ShowMessage("Du använde " + potion.Name + " för att återställa " + potion.AmountToHeal.ToString() + " poäng hälsa");
                    UpdateUI();
                }

                else if (Action == 3)
                {
                    Spell spell = (Spell)cbo_Spells.SelectedItem;
                    if (_player.CurrentMana >= spell.ManaCost)
                    {
                        if (spell.EffectID == World.SPELL_EFFECT_ID_DAMAGE)
                        {
                           
                            damageDealt = spell.EffectAmount;
                            damageDealt += _player.Level - _currentMonster.Level;
                            if (_currentMonster.ArmourUsed != null)
                            {
                                damageDealt -= _currentMonster.ArmourUsed.MagicResistance / 2;
                            }
                            if(damageDealt <= 0)
                            {
                                ShowMessage("Din magiska formel utgjorde ingen skada!");
                            }
                            else
                            {
                               _currentMonster.DealDamage(damageDealt);
                                ShowMessage("Du gav " + damageDealt.ToString() + " poäng magisk skada på fienden");
                            }
                        }
                        else if(spell.EffectID == World.SPELL_EFFECT_ID_HEAL)
                        {
                            _player.Heal(spell.EffectAmount);
                            ShowMessage("Du helade dig själv för " + spell.EffectAmount.ToString() + " attackpoäng");
                        }
                        else if(spell.EffectID == World.SPELL_EFFECT_ID_RESTORE_MANA)
                        {
                            _player.RestoreMana(spell.EffectAmount);
                            ShowMessage("Du återställde " + spell.EffectAmount.ToString() + " mana!");
                        }
                        else
                        {
                            ShowMessage("Din magiska formel gjorde ingen nytta");
                        }
                        _player.DrainMana(spell.ManaCost);
                    }
                }


                if (_currentMonster.CurrentHitPoints <= 0)
                {
                    ShowMessage("");
                    ShowMessage("Du besegrade " + _currentMonster.Name + ".");
                    EndCombat(_currentMonster);
                    _player.Heal(_player.MaximumHitPoints);
                    MoveTo(_player.CurrentLocation);
                }                
                else
                {
                    if (RandomNumber.Between(_currentMonster.Level, _currentMonster.Level + 5) -( _player.Level) <= 0)
                    {
                        ShowMessage("Du lyckades ducka fiendens attack!");
                    }
                    else
                    {
                        bool magicAttack = false;
                        Spell spellToCast = null;

                        if (_currentMonster.Spells.Count != 0)
                        {
                            int rndNumber = RandomNumber.Between(1, 3);
                            int highestDamageSpell = 0;
                            if(rndNumber > 1 || _currentMonster.Intelligent > _currentMonster.Strength)
                            {
                                foreach(SpellList sp in _currentMonster.Spells)
                                {
                                    if(sp.Details.CombatSpell = true && _currentMonster.CurrentMana >= sp.Details.ManaCost)
                                    {
                                        if (sp.Details.EffectAmount > highestDamageSpell)
                                        {
                                            spellToCast = sp.Details;
                                            highestDamageSpell = sp.Details.EffectAmount;
                                            magicAttack = true;
                                        } 
                                    }
                                }
                            }
                        }

                        if (magicAttack == true)
                        {
                            damageDealt = spellToCast.EffectAmount;
                            damageDealt += _currentMonster.Level - _player.Level;
                            _currentMonster.DrainMana(spellToCast.ManaCost);

                            if (_player.ArmourUsed != null)
                            {
                                damageDealt -= _player.ArmourUsed.MagicResistance / 2;
                            }

                            ShowMessage(_currentMonster.Name + " utgjorde " + damageDealt.ToString() + " poäng magisk skada!");
                        }
                        else
                        {
                            damageDealt = RandomNumber.Between(1, _currentMonster.MaximumDamage);
                            damageDealt += _currentMonster.Level - _player.Level;
                            ShowMessage(_currentMonster.Name + " utgjorde " + damageDealt.ToString() + " poäng skada!");
                        }
                        _player.DealDamage(damageDealt);
                    }

                    if (_player.CurrentHitPoints <= 0)
                    {
                        InCombat = false;
                        _player.Heal(_player.MaximumHitPoints);
                        MoveTo(World.LocationByID(World.LOCATION_ID_HOME));
                        ShowMessage("DU HAR DÖTT!");
                        ShowMessage(" ");
                        ShowMessage("Du återuppstod. Du befinner dig nu hemma i ditt hus.");
                    }
                }
                UpdateUI();
            }
        }
        private void btnUseWeapon_Click(object sender, EventArgs e)
        {
            CombatPhase(1);
        }

        private void btnUsePotion_Click(object sender, EventArgs e)
        {
            CombatPhase(2);
        }

        private void cboWeapons_SelectedIndexChanged(object sender, EventArgs e)
        {
            _player.CurrentWeapon = (Weapon)cbo_Weapons.SelectedItem;

        }

        private void cboPotions_SelectedIndexChanged(object sender, EventArgs e)
        {
            _player.CurrentPotion = (HealingPotion)cbo_Potions.SelectedItem;
        }

        private void btnUseSpell_Click(object sender, EventArgs e)
        {
            Spell spell = (Spell)cbo_Spells.SelectedItem;
            if(InCombat == true)
            {
                if(spell.CombatSpell == false)
                {
                    ShowMessage("You can't use this spell in combat");
                }
                else
                {
                    if (_player.CurrentMana >= spell.ManaCost)
                    {
                        CombatPhase(3);
                    }
                    else
                    {
                        ShowMessage("You don't have enough mana to use this spell");
                    }
                }
            }
            else { 
                if(spell.CombatSpell == true)
                {
                    ShowMessage("You must be in combat to use this spell");
                }
                else
                {
                    // custom effect code
                    if(spell.ManaCost > _player.CurrentMana)
                    {
                        ShowMessage("You don't have enough mana to cast this");
                    }
                    else
                    {
                        if (spell.ID == World.SPELL_ID_TELEPORT)
                        {
                            ShowMessage("Teleportation spell used");
                            ShowMessage("");
                            ShowMessage("Teleported back home");
                            _player.DrainMana(spell.ManaCost);
                            MoveTo(World.LocationByID(World.LOCATION_ID_HOME));
                        }
                     /*   }else if(spell.EffectID == World.SPELL_EFFECT_ID_RAISE_HP)
                        {
                            ShowMessage("Maximum hit points increased!");
                            ShowMessage("This effect will wear off when you defeat a monster");
                            _player.Buffs.Add(new BuffsList(World.BuffByID(World.BUFF_ID_INCREASE_MAX_HP),20));
                            _player.UpdateMaximumStats();
                            _player.Heal(spell.EffectAmount);
                          //  _player.Heal()

                        }*/
                    }
                }
            }
        }

        private void FormAdventurePlus_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        private void FormAdventurePlus_Load(object sender, EventArgs e)
        {

        }

        private void FormAdventurePlus_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.WriteAllText(saveFileName, _player.toXmlString());
        }

    }
}
