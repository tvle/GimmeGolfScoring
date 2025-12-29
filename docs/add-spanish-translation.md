# Quick Guide: Adding Spanish Translation

## Step 1: Create Spanish Resource File

1. In Visual Studio, right-click on `Resources/Strings` folder
2. Select **Add** > **New Item**
3. Search for "**Resources File**"
4. Name it: **`AppResources.es.resx`**
5. Click **Add**

## Step 2: Copy Keys and Translate Values

Open both `AppResources.resx` and `AppResources.es.resx` side by side.

For each entry in AppResources.resx, copy the **Name** and translate the **Value**:

### Common UI Strings
| Name | English | Spanish |
|------|---------|---------|
| AppName | iDoublePress | iDoublePress |
| Cancel | Cancel | Cancelar |
| OK | OK | OK |
| Yes | Yes | Sí |
| No | No | No |
| Error | Error | Error |
| Loading | Loading... | Cargando... |

### Golf Round Management
| Name | English | Spanish |
|------|---------|---------|
| Resume | Resume | Reanudar |
| StartNew | Start New | Comenzar Nueva |
| ResumeRoundTitle | Resume Round? | ¿Reanudar Ronda? |
| MultipleRoundsTitle | You have {0} rounds in progress | Tienes {0} rondas en progreso |
| StartNewRound | Start New Round | Comenzar Nueva Ronda |
| SelectCourse | Select Course | Seleccionar Campo |

### Complete/Abandon Round
| Name | English | Spanish |
|------|---------|---------|
| CompleteRoundTitle | Complete Round? | ¿Completar Ronda? |
| CompleteRoundMessage | Your final score is {0} ({1}). Mark this round as complete? | Tu puntuación final es {0} ({1}). ¿Marcar esta ronda como completa? |
| CompleteRound | Complete Round | Completar Ronda |
| RoundCompleted | Round completed! Score: {0} | ¡Ronda completada! Puntuación: {0} |
| AbandonRoundTitle | Abandon Round? | ¿Abandonar Ronda? |
| AbandonRoundMessage | Are you sure you want to abandon this round? It will not be saved. | ¿Estás seguro de que quieres abandonar esta ronda? No se guardará. |
| YesAbandon | Yes, Abandon | Sí, Abandonar |
| Abandon | Abandon | Abandonar |

### Active Round Page
| Name | English | Spanish |
|------|---------|---------|
| ActiveRound | Active Round | Ronda Activa |
| Previous | Previous | Anterior |
| Next | Next | Siguiente |
| HoleFormat | Hole {0} | Hoyo {0} |
| ParFormat | Par {0} | Par {0} |

### Error Messages
| Name | English | Spanish |
|------|---------|---------|
| RoundNotFound | Round not found or has no holes | Ronda no encontrada o sin hoyos |
| NoPlayerFound | No player found. Please restart the app. | No se encontró jugador. Por favor reinicia la aplicación. |

### Time Formatting
| Name | English | Spanish |
|------|---------|---------|
| JustNow | Just now | Justo ahora |
| MinutesAgo | {0} min ago | hace {0} min |
| HourAgo | {0} hour ago | hace {0} hora |
| HoursAgo | {0} hours ago | hace {0} horas |
| DayAgo | {0} day ago | hace {0} día |
| DaysAgo | {0} days ago | hace {0} días |

### Course/Player Names
| Name | English | Spanish |
|------|---------|---------|
| StandardCourse | Standard Course | Campo Estándar |
| NineHoleCourse | 9-Hole Course | Campo de 9 Hoyos |
| PracticeCourse | Practice Course | Campo de Práctica |
| DefaultLocation | Default | Predeterminado |
| Me | Me | Yo |

### Seed Data Messages
| Name | English | Spanish |
|------|---------|---------|
| DefaultPlayerCreated | Default player created | Jugador predeterminado creado |
| Par72CourseCreated | Par 72 course created | Campo Par 72 creado |
| Par36CourseCreated | Par 36 course created | Campo Par 36 creado |
| PracticeCourseCreated | Practice course created | Campo de práctica creado |

### Golf Scoring Terms
| Name | English | Spanish |
|------|---------|---------|
| Eagle | Eagle | Eagle |
| Birdie | Birdie | Birdie |
| Par | Par | Par |
| Bogey | Bogey | Bogey |
| DoubleBogey | Double Bogey | Doble Bogey |
| TripleBogey | Triple Bogey | Triple Bogey |

### Score Display
| Name | English | Spanish |
|------|---------|---------|
| ScoreEven | E | E |
| ScoreUnder | -{0} | -{0} |
| ScoreOver | +{0} | +{0} |

### Navigation
| Name | English | Spanish |
|------|---------|---------|
| Home | Home | Inicio |
| Rounds | Rounds | Rondas |
| Statistics | Statistics | Estadísticas |
| Settings | Settings | Configuración |

### Language Selection
| Name | English | Spanish |
|------|---------|---------|
| SelectLanguage | Select Language | Seleccionar Idioma |
| LanguageChanged | Language Changed | Idioma Cambiado |
| RestartAppMessage | Please restart the app for the language change to take full effect. | Por favor reinicia la aplicación para que el cambio de idioma tenga efecto completo. |

## Step 3: Important Translation Notes

### Keep Format Placeholders
When translating strings with `{0}`, `{1}`, etc., **keep these placeholders exactly as they are**:

? Wrong: "Tu puntuación es 72" (removed {0})  
? Correct: "Tu puntuación es {0}"

### Golf Terms
Many golf terms are international and can stay in English or use local variants:
- **Eagle** - Usually kept as "Eagle" in Spanish
- **Birdie** - Usually kept as "Birdie"  
- **Par** - Always "Par"
- **Bogey** - Usually "Bogey"

### Formal vs Informal
Spanish has formal (usted) and informal (tú) forms. For a golf app:
- Use **informal (tú)** for friendly tone
- Examples: "¿Quieres...?" instead of "¿Desea...?"

## Step 4: Test Spanish Translation

1. **Change device language** to Spanish in Settings
2. **Restart the app**
3. **Verify** all screens show Spanish text

Or programmatically:
```csharp
LocalizationManager.Instance.SetCulture(new CultureInfo("es"));
LocalizationManager.Instance.SaveLanguagePreference(new CultureInfo("es"));
```

## Step 5: Build and Verify

```bash
dotnet build
```

Should build successfully with no errors.

## Quick Copy-Paste Spanish .resx Entries

```xml
<data name="Resume" xml:space="preserve">
  <value>Reanudar</value>
</data>
<data name="StartNew" xml:space="preserve">
  <value>Comenzar Nueva</value>
</data>
<data name="CompleteRound" xml:space="preserve">
  <value>Completar Ronda</value>
</data>
<data name="Abandon" xml:space="preserve">
  <value>Abandonar</value>
</data>
<data name="ActiveRound" xml:space="preserve">
  <value>Ronda Activa</value>
</data>
<data name="Previous" xml:space="preserve">
  <value>Anterior</value>
</data>
<data name="Next" xml:space="preserve">
  <value>Siguiente</value>
</data>
```

## Common Spanish Golf Terms

- **Hoyo** = Hole
- **Ronda** = Round
- **Campo** = Course
- **Puntuación** = Score
- **Golpe** = Stroke
- **Calle** = Fairway
- **Green** = Green (usually kept in English)
- **Putt** = Putt (usually kept in English)
- **Handicap** = Hándicap
- **Torneo** = Tournament

## Need Help with Other Languages?

Use the same process for:
- **French**: AppResources.fr.resx
- **German**: AppResources.de.resx
- **Japanese**: AppResources.ja.resx
- **Korean**: AppResources.ko.resx
- **Chinese**: AppResources.zh-CN.resx

The app will automatically detect and use the appropriate language file based on the device's language setting!
